using System.Security.Claims;
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
        private readonly IFamilyRepository _familyRepository;

        public MealPlansController(
            IMealPlanRepository mealPlanRepository,
            INonServingDayRepository nonServingDayRepository,
            IAppSettingsRepository appSettingsRepository,
            IAuditLogRepository auditLogRepository,
            IFamilyRepository familyRepository)
        {
            _familyRepository = familyRepository;
            _mealPlanRepository = mealPlanRepository;
            _nonServingDayRepository = nonServingDayRepository;
            _appSettingsRepository = appSettingsRepository;
            _auditLogRepository = auditLogRepository;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MealPlan>> GetById(ulong id)
        {
            var mealPlan = await _mealPlanRepository.GetByIdAsync(id);
            if (mealPlan is null || (!mealPlan.IsSpecialDay && await CallerSeesSpecialDaysOnlyAsync()))
            {
                return NotFound();
            }
            return Ok(mealPlan);
        }

        /// <summary>Admin view -- the full range entered so far, unrestricted by the member visibility window.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MealPlan>>> GetRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            if (to < from)
            {
                return BadRequest("'to' must not be before 'from'.");
            }
            var meals = await _mealPlanRepository.GetRangeAsync(from, to);
            return Ok(await CallerSeesSpecialDaysOnlyAsync() ? meals.Where(m => m.IsSpecialDay) : meals);
        }

        /// <summary>Member-facing view -- today through AppSettings.MealVisibilityDays ahead, capped by whatever the Admin has actually entered.</summary>
        [HttpGet("upcoming")]
        public async Task<ActionResult<IEnumerable<MealPlan>>> GetUpcoming()
        {
            var settings = await _appSettingsRepository.GetAsync();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var to = today.AddDays(Math.Max(0, (int)settings.MealVisibilityDays - 1));
            var meals = await _mealPlanRepository.GetRangeAsync(today, to);
            // A family that doesn't take the regular meal only ever sees the Special Days.
            return Ok(await CallerSeesSpecialDaysOnlyAsync() ? meals.Where(m => m.IsSpecialDay) : meals);
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<MealPlan>> Create(CreateMealPlanRequest request)
        {
            if (request.SpecialDayName is { Length: > 100 })
            {
                return BadRequest("Special Day name must be 100 characters or fewer.");
            }

            // A Special Day overrides Sunday / Ramadan / non-serving days, so only an ordinary day needs the check.
            var validationError = request.IsSpecialDay ? null : await ValidateServingDateAsync(request.MealDate);
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
                MetadataJson = request.IsSpecialDay ? System.Text.Json.JsonSerializer.Serialize(new { request.IsSpecialDay, request.SpecialDayName }) : null,
                CreatedAt = DateTime.UtcNow
            });

            var created = await _mealPlanRepository.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(ulong id, UpdateMealPlanRequest request)
        {
            if (request.SpecialDayName is { Length: > 100 })
            {
                return BadRequest("Special Day name must be 100 characters or fewer.");
            }

            // Un-marking a Special Day turns it back into an ordinary day, so its date must be a
            // normal serving day (a Special Day on a Sunday can't simply be un-marked).
            if (request.IsSpecialDay == false)
            {
                var existing = await _mealPlanRepository.GetByIdAsync(id);
                if (existing is null)
                {
                    return NotFound();
                }
                var validationError = await ValidateServingDateAsync(existing.MealDate);
                if (validationError is not null)
                {
                    return BadRequest($"This date can't be an ordinary meal day -- {validationError} Delete the meal instead, or keep it as a Special Day.");
                }
            }

            var updated = await _mealPlanRepository.UpdateAsync(id, request);
            if (!updated)
            {
                return NotFound();
            }

            if (request.IsSpecialDay.HasValue)
            {
                await _auditLogRepository.AddAsync(new AuditLog
                {
                    UserId = request.UpdatedByUserId,
                    Action = request.IsSpecialDay.Value ? "MealPlanMarkedSpecialDay" : "MealPlanUnmarkedSpecialDay",
                    EntityType = "MealPlan",
                    EntityId = id,
                    CreatedAt = DateTime.UtcNow
                });
            }

            return NoContent();
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

                if (item.SpecialDayName is { Length: > 100 })
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "Special Day name must be 100 characters or fewer.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                var existing = await _mealPlanRepository.GetByDateAsync(item.MealDate);

                // Will this date be a Special Day after the import? An explicit Yes/No in the sheet wins;
                // a blank cell keeps whatever the date already is (and is "No" for a brand-new date).
                var willBeSpecial = item.IsSpecialDay ?? existing?.IsSpecialDay ?? false;
                var validationError = willBeSpecial ? null : await ValidateServingDateAsync(item.MealDate);
                if (validationError is not null)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = validationError;
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                if (existing is not null)
                {
                    await _mealPlanRepository.UpdateAsync(existing.Id, new UpdateMealPlanRequest
                    {
                        MealDescription = item.MealDescription,
                        IsSpecialDay = item.IsSpecialDay,
                        SpecialDayName = item.SpecialDayName,
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
                        IsSpecialDay = item.IsSpecialDay ?? false,
                        SpecialDayName = item.SpecialDayName,
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

        /// <summary>True when the caller is a Family Head whose family doesn't take the regular meal -- they may only see Special Days. Admins and regular families see everything.</summary>
        private async Task<bool> CallerSeesSpecialDaysOnlyAsync()
        {
            if (User?.IsInRole(RoleNames.FamilyHead) != true)
            {
                return false;
            }
            if (!ulong.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return false;
            }
            var family = await _familyRepository.GetByFamilyHeadUserIdAsync(userId);
            return family is not null && !family.TakesRegularMeal;
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
