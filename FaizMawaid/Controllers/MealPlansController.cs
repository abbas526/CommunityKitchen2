using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MealPlansController : ControllerBase
    {
        private readonly IMealPlanRepository _mealPlanRepository;
        private readonly INonServingDayRepository _nonServingDayRepository;
        private readonly IAppSettingsRepository _appSettingsRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public MealPlansController(
            IMealPlanRepository mealPlanRepository,
            INonServingDayRepository nonServingDayRepository,
            IAppSettingsRepository appSettingsRepository,
            IAuditLogRepository auditLogRepository)
        {
            _mealPlanRepository = mealPlanRepository;
            _nonServingDayRepository = nonServingDayRepository;
            _appSettingsRepository = appSettingsRepository;
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlan>> GetById(ulong id)
        {
            var mealPlan = await _mealPlanRepository.GetByIdAsync(id);
            return mealPlan is null ? NotFound() : Ok(mealPlan);
        }

        /// <summary>Admin view -- the full range entered so far, unrestricted by the member visibility window.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MealPlan>>> GetRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            if (to < from)
            {
                return BadRequest("'to' must not be before 'from'.");
            }
            return Ok(await _mealPlanRepository.GetRangeAsync(from, to));
        }

        /// <summary>Member-facing view -- today through AppSettings.MealVisibilityDays ahead, capped by whatever the Admin has actually entered.</summary>
        [HttpGet("upcoming")]
        public async Task<ActionResult<IEnumerable<MealPlan>>> GetUpcoming()
        {
            var settings = await _appSettingsRepository.GetAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var to = today.AddDays(Math.Max(0, (int)settings.MealVisibilityDays - 1));
            return Ok(await _mealPlanRepository.GetRangeAsync(today, to));
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<MealPlan>> Create(CreateMealPlanRequest request)
        {
            var validationError = await ValidateServingDateAsync(request.MealDate);
            if (validationError is not null)
            {
                return BadRequest(validationError);
            }

            var existing = await _mealPlanRepository.GetByDateAsync(request.MealDate);
            if (existing is not null)
            {
                return Conflict($"A meal is already planned for {request.MealDate:yyyy-MM-dd}. Use PUT to edit it.");
            }

            var id = await _mealPlanRepository.CreateAsync(request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "MealPlanCreated",
                EntityType = "MealPlan",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _mealPlanRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(ulong id, UpdateMealPlanRequest request)
        {
            var updated = await _mealPlanRepository.UpdateAsync(id, request);
            return updated ? NoContent() : NotFound();
        }

        /// <summary>
        /// Bulk-imports meal plans from the rows the client parsed out of an uploaded Excel
        /// file. Each row is validated and saved independently -- a bad date (Sunday, a
        /// non-serving day, or one this batch already used) is skipped with a reason rather
        /// than failing the whole upload. A date that already has a meal planned is
        /// overwritten (Status "Updated") rather than rejected, since re-uploading a corrected
        /// sheet for the same week is an expected use of this feature.
        /// </summary>
        [HttpPost("bulk-import")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<BulkMealPlanResponse>> BulkImport(BulkMealPlanRequest request)
        {
            var response = new BulkMealPlanResponse();
            var seenDates = new HashSet<DateOnly>();

            foreach (var item in request.Items)
            {
                var rowResult = new BulkMealPlanRowResult { MealDate = item.MealDate.ToString("yyyy-MM-dd") };

                if (string.IsNullOrWhiteSpace(item.MealDescription))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "Meal description is required.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }
                if (item.MealDescription.Length > 500)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "Meal description must be 500 characters or fewer.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }
                if (!seenDates.Add(item.MealDate))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "This date appears more than once in the uploaded file.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                var validationError = await ValidateServingDateAsync(item.MealDate);
                if (validationError is not null)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = validationError;
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                var existing = await _mealPlanRepository.GetByDateAsync(item.MealDate);
                if (existing is not null)
                {
                    await _mealPlanRepository.UpdateAsync(existing.Id, new UpdateMealPlanRequest
                    {
                        MealDescription = item.MealDescription,
                        UpdatedByUserId = request.CreatedByUserId
                    });
                    rowResult.Status = "Updated";
                    response.UpdatedCount++;
                }
                else
                {
                    await _mealPlanRepository.CreateAsync(new CreateMealPlanRequest
                    {
                        MealDate = item.MealDate,
                        MealDescription = item.MealDescription,
                        CreatedByUserId = request.CreatedByUserId
                    });
                    rowResult.Status = "Created";
                    response.CreatedCount++;
                }

                response.Rows.Add(rowResult);
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByUserId,
                Action = "MealPlanBulkImport",
                EntityType = "MealPlan",
                MetadataJson = System.Text.Json.JsonSerializer.Serialize(new { response.CreatedCount, response.UpdatedCount, response.SkippedCount }),
                CreatedAt = DateTime.UtcNow
            });

            return Ok(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Delete(ulong id)
        {
            var deleted = await _mealPlanRepository.DeleteAsync(id);
            return deleted ? NoContent() : NotFound();
        }

        private async Task<string?> ValidateServingDateAsync(DateOnly date)
        {
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                return "The kitchen doesn't serve food on Sundays.";
            }

            if (MisriCalendar.IsRamadan(date))
            {
                return "The kitchen doesn't serve food during the month of Ramadan.";
            }

            if (await _nonServingDayRepository.IsNonServingDayAsync(date))
            {
                return $"{date:yyyy-MM-dd} is marked as a non-serving day.";
            }

            return null;
        }
    }
}
