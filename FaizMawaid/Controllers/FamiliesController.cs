using System.Security.Claims;
using System.Text.Json;
using FaizMawaid.Models;
using FaizMawaid.Models.Dtos;
using FaizMawaid.Repositories.Interfaces;
using FaizMawaid.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace FaizMawaid.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // NOTE: this is deliberately a bare [Authorize] (not [Authorize(Roles = RoleNames.Admin)]).
    // ASP.NET Core combines a class-level [Authorize] with a method-level one using AND, not
    // override -- so a class-level Roles=Admin restriction here would silently still apply even
    // to actions below carrying just a plain [Authorize], defeating the point of opening those to
    // any signed-in Family Head. Every action that should stay Admin-only below therefore carries
    // its own explicit [Authorize(Roles = RoleNames.Admin)].
    [Authorize]
    public class FamiliesController : ControllerBase
    {
        private readonly IFamilyRepository _familyRepository;
        private readonly IFamilySizeHistoryRepository _familySizeHistoryRepository;
        private readonly IThaaliSizeRepository _thaaliSizeRepository;
        private readonly IAreaRepository _areaRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IPasswordHasherService _passwordHasher;

        public FamiliesController(
            IFamilyRepository familyRepository,
            IFamilySizeHistoryRepository familySizeHistoryRepository,
            IThaaliSizeRepository thaaliSizeRepository,
            IAreaRepository areaRepository,
            IUserRepository userRepository,
            IAuditLogRepository auditLogRepository,
            IPasswordHasherService passwordHasher)
        {
            _familyRepository = familyRepository;
            _familySizeHistoryRepository = familySizeHistoryRepository;
            _thaaliSizeRepository = thaaliSizeRepository;
            _areaRepository = areaRepository;
            _userRepository = userRepository;
            _auditLogRepository = auditLogRepository;
            _passwordHasher = passwordHasher;
        }

        [HttpGet]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<Family>>> GetAll([FromQuery] RegistrationStatus? status)
        {
            return Ok(await _familyRepository.GetAllAsync(status));
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<Family>> GetById(ulong id)
        {
            var family = await _familyRepository.GetByIdAsync(id);
            return family is null ? NotFound() : Ok(family);
        }

        /// <summary>Family Head self-registration: creates the User + Family (Pending) together. Open to anyone -- there's no account yet to authenticate this with.</summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<RegisterFamilyResponse>> Register(RegisterFamilyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SabilNumber))
            {
                return BadRequest("Sabil Number is required.");
            }
            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                return BadRequest("Password must be at least 8 characters.");
            }

            var size = await _thaaliSizeRepository.GetByIdAsync(request.ThaaliSizeId);
            if (size is null)
            {
                return BadRequest($"ThaaliSizeId {request.ThaaliSizeId} does not exist.");
            }

            if (request.AreaId.HasValue)
            {
                var area = await _areaRepository.GetByIdAsync(request.AreaId.Value);
                if (area is null)
                {
                    return BadRequest($"AreaId {request.AreaId} does not exist.");
                }
            }

            var existingEmail = await _userRepository.GetByEmailAsync(request.Email);
            if (existingEmail is not null)
            {
                return Conflict($"A user with email '{request.Email}' already exists.");
            }

            var existingSabil = await _userRepository.GetBySabilNumberAsync(request.SabilNumber);
            if (existingSabil is not null)
            {
                return Conflict($"Sabil Number '{request.SabilNumber}' is already registered to another family.");
            }

            request.PasswordHash = _passwordHasher.Hash(request.Password);

            RegisterFamilyResponse result;
            try
            {
                result = await _familyRepository.RegisterAsync(request);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Backstop for a race between the checks above and the insert -- extremely
                // unlikely at this app's scale, but cheap to guard against.
                var duplicateField = ex.Message.Contains("SabilNumber", StringComparison.OrdinalIgnoreCase) ? "Sabil Number" : "email";
                return Conflict($"That {duplicateField} is already registered to another family.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = result.UserId,
                Action = "FamilyRegistered",
                EntityType = "Family",
                EntityId = result.FamilyId,
                CreatedAt = DateTime.UtcNow
            });

            return CreatedAtAction(nameof(GetById), new { id = result.FamilyId }, result);
        }

        [HttpPut("{id}/approve")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Approve(ulong id, ApproveFamilyRequest request)
        {
            var approved = await _familyRepository.ApproveAsync(id, request.ApprovedByAdminUserId);
            if (!approved)
            {
                return Conflict("Family was not found, or is not in Pending status.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ApprovedByAdminUserId,
                Action = "FamilyApproved",
                EntityType = "Family",
                EntityId = id,
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        [HttpPut("{id}/reject")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Reject(ulong id, RejectFamilyRequest request)
        {
            var rejected = await _familyRepository.RejectAsync(id, request.RejectedByAdminUserId);
            if (!rejected)
            {
                return Conflict("Family was not found, or is not in Pending status.");
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.RejectedByAdminUserId,
                Action = "FamilyRejected",
                EntityType = "Family",
                EntityId = id,
                MetadataJson = request.Reason is null ? null : JsonSerializer.Serialize(new { request.Reason }),
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Update(ulong id, UpdateFamilyRequest request)
        {
            // Only fetch the "before" row when the regular-meal flag is actually being set, so the
            // audit trail records a real change (and ordinary address/area edits stay a single call).
            var before = request.TakesRegularMeal.HasValue ? await _familyRepository.GetByIdAsync(id) : null;

            var updated = await _familyRepository.UpdateAsync(id, request);
            if (!updated)
            {
                return NotFound();
            }

            if (request.TakesRegularMeal.HasValue && (before is null || before.TakesRegularMeal != request.TakesRegularMeal.Value))
            {
                var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                await _auditLogRepository.AddAsync(new AuditLog
                {
                    UserId = ulong.TryParse(userIdClaim, out var actingUserId) ? actingUserId : null,
                    Action = "FamilyTakesRegularMealChanged",
                    EntityType = "Family",
                    EntityId = id,
                    MetadataJson = JsonSerializer.Serialize(new { request.TakesRegularMeal }),
                    CreatedAt = DateTime.UtcNow
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Admin-only direct size change (bypasses the ThaaliSizeChangeRequests review
        /// queue), e.g. for a correction from admin/families.html's pencil-icon edit --
        /// mirrors how Admin can still edit a family's Address/Area directly too,
        /// alongside the family-initiated request flow. A Family Head submits a size
        /// change via POST /api/thaalisizechangerequests instead, as of 2026-09-27 --
        /// see ThaaliSizeChangeRequestsController.
        /// </summary>
        [HttpPut("{id}/thaali-size")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> ChangeThaaliSize(ulong id, ChangeThaaliSizeRequest request)
        {
            var size = await _thaaliSizeRepository.GetByIdAsync(request.NewThaaliSizeId);
            if (size is null)
            {
                return BadRequest($"ThaaliSizeId {request.NewThaaliSizeId} does not exist.");
            }

            var updated = await _familyRepository.ChangeThaaliSizeAsync(id, request);
            if (!updated)
            {
                return NotFound();
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ChangedByUserId,
                Action = "FamilyThaaliSizeChanged",
                EntityType = "Family",
                EntityId = id,
                MetadataJson = JsonSerializer.Serialize(new { request.NewThaaliSizeId, request.EffectiveFromDate }),
                CreatedAt = DateTime.UtcNow
            });

            return NoContent();
        }

        [HttpGet("{id}/size-history")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<FamilySizeHistory>>> GetSizeHistory(ulong id)
        {
            return Ok(await _familySizeHistoryRepository.GetByFamilyIdAsync(id));
        }

        [HttpPut("{id}/deactivate")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Deactivate(ulong id)
        {
            var updated = await _familyRepository.SetActiveAsync(id, false);
            return updated ? NoContent() : NotFound();
        }

        [HttpPut("{id}/activate")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<IActionResult> Activate(ulong id)
        {
            var updated = await _familyRepository.SetActiveAsync(id, true);
            return updated ? NoContent() : NotFound();
        }

        /// <summary>Lists the sub-families (additional linked households) an Admin has attached to this primary family.</summary>
        [HttpGet("{id}/sub-families")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<IEnumerable<Family>>> GetSubFamilies(ulong id)
        {
            var parent = await _familyRepository.GetByIdAsync(id);
            if (parent is null)
            {
                return NotFound();
            }

            return Ok(await _familyRepository.GetSubFamiliesAsync(id));
        }

        /// <summary>
        /// Admin-only: links a new sub-family (e.g. a married child's household in a nearby
        /// flat/building) to this primary family so it can take its own Thaali. Enforced via the
        /// explicit [Authorize(Roles = Admin)] below; CreatedByAdminUserId is additionally
        /// validated to belong to a real Admin, matching the rest of this app's established
        /// pattern of trusting body-supplied acting-user ids rather than re-deriving them from
        /// claims (see ApprovedByAdminUserId, RejectedByAdminUserId elsewhere).
        /// </summary>
        [HttpPost("{id}/sub-families")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<Family>> CreateSubFamily(ulong id, CreateSubFamilyRequest request)
        {
            var parent = await _familyRepository.GetByIdAsync(id);
            if (parent is null)
            {
                return NotFound();
            }
            if (parent.ParentFamilyId is not null)
            {
                return BadRequest("A sub-family can't itself have a sub-family -- link the new household to the primary family instead.");
            }

            if (string.IsNullOrWhiteSpace(request.SubFamilyLabel))
            {
                return BadRequest("A short label is required to identify the sub-family (e.g. \"Son's family - Building B\").");
            }

            var admin = await _userRepository.GetByIdAsync(request.CreatedByAdminUserId);
            if (admin is null || (admin.RoleId != RoleIds.Admin && admin.RoleId != RoleIds.SuperAdmin))
            {
                return BadRequest("Only an Admin can add a sub-family.");
            }

            var size = await _thaaliSizeRepository.GetByIdAsync(request.ThaaliSizeId);
            if (size is null)
            {
                return BadRequest($"ThaaliSizeId {request.ThaaliSizeId} does not exist.");
            }

            var subFamilyId = await _familyRepository.CreateSubFamilyAsync(id, request);

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.CreatedByAdminUserId,
                Action = "SubFamilyCreated",
                EntityType = "Family",
                EntityId = subFamilyId,
                MetadataJson = JsonSerializer.Serialize(new { ParentFamilyId = id, request.SubFamilyLabel }),
                CreatedAt = DateTime.UtcNow
            });

            var created = await _familyRepository.GetByIdAsync(subFamilyId);
            return CreatedAtAction(nameof(GetById), new { id = subFamilyId }, created);
        }

        /// <summary>
        /// Admin-only: bulk-imports many families at once from the rows the client parsed out
        /// of an uploaded Excel file. Each row is validated and created independently -- one bad
        /// row (missing field, weak password, unknown Thaali size name, duplicate email/Sabil
        /// Number) is skipped with a reason rather than failing the whole batch. Imported
        /// families are created already Approved and active (see ImportApprovedFamilyAsync) --
        /// the Admin is vouching for every row by uploading it, matching how Sub-Families work.
        /// </summary>
        [HttpPost("bulk-import")]
        [Authorize(Roles = RoleNames.Admin)]
        public async Task<ActionResult<BulkFamilyImportResponse>> BulkImport(BulkFamilyImportRequest request)
        {
            var response = new BulkFamilyImportResponse();
            var sizes = (await _thaaliSizeRepository.GetAllAsync()).ToList();
            var areas = (await _areaRepository.GetAllAsync()).ToList();
            var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSabilNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in request.Items)
            {
                var rowResult = new BulkFamilyImportRowResult { Email = item.Email };

                if (string.IsNullOrWhiteSpace(item.FullName) || string.IsNullOrWhiteSpace(item.Email) ||
                    string.IsNullOrWhiteSpace(item.SabilNumber) || string.IsNullOrWhiteSpace(item.Password))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "Full Name, Email, Sabil Number, and Password are all required.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }
                if (item.Password.Length < 8)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "Password must be at least 8 characters.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }
                if (!seenEmails.Add(item.Email))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "This email appears more than once in the uploaded file.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }
                if (!seenSabilNumbers.Add(item.SabilNumber))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "This Sabil Number appears more than once in the uploaded file.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                var size = sizes.FirstOrDefault(s => string.Equals(s.Name, item.ThaaliSizeName?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (size is null)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = $"Thaali Size '{item.ThaaliSizeName}' doesn't match any size defined in the system.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                if (!TryParseYesNo(item.TakesRegularMeal, defaultValue: true, out var takesRegularMeal))
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = $"'Take Thaali Regularly' must be Yes or No (or left blank for Yes), but was '{item.TakesRegularMeal}'.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                byte? areaId = null;
                if (!string.IsNullOrWhiteSpace(item.AreaName))
                {
                    var area = areas.FirstOrDefault(a => string.Equals(a.Name, item.AreaName.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (area is null)
                    {
                        rowResult.Status = "Skipped";
                        rowResult.Message = $"Area '{item.AreaName}' doesn't match any Area defined in the system.";
                        response.SkippedCount++;
                        response.Rows.Add(rowResult);
                        continue;
                    }
                    areaId = area.Id;
                }

                var existingEmail = await _userRepository.GetByEmailAsync(item.Email);
                if (existingEmail is not null)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "A user with this email already exists.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                var existingSabil = await _userRepository.GetBySabilNumberAsync(item.SabilNumber);
                if (existingSabil is not null)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = $"Sabil Number '{item.SabilNumber}' is already registered to another user.";
                    response.SkippedCount++;
                    response.Rows.Add(rowResult);
                    continue;
                }

                try
                {
                    var registerRequest = new RegisterFamilyRequest
                    {
                        Email = item.Email,
                        SabilNumber = item.SabilNumber,
                        PasswordHash = _passwordHasher.Hash(item.Password),
                        FullName = item.FullName,
                        Phone = item.Phone,
                        Address = item.Address,
                        AreaId = areaId,
                        TakesRegularMeal = takesRegularMeal,
                        NumberOfMembers = item.NumberOfMembers,
                        ThaaliSizeId = size.Id
                    };
                    await _familyRepository.ImportApprovedFamilyAsync(registerRequest, request.ImportedByAdminUserId);
                    rowResult.Status = "Created";
                    response.CreatedCount++;
                }
                catch (MySqlException ex) when (ex.Number == 1062)
                {
                    rowResult.Status = "Skipped";
                    rowResult.Message = "That email or Sabil Number was already registered (possibly by another row just above in this same upload).";
                    response.SkippedCount++;
                }

                response.Rows.Add(rowResult);
            }

            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = request.ImportedByAdminUserId,
                Action = "FamilyBulkImport",
                EntityType = "Family",
                MetadataJson = JsonSerializer.Serialize(new { response.CreatedCount, response.SkippedCount }),
                CreatedAt = DateTime.UtcNow
            });

            return Ok(response);
        }

        /// <summary>Parses a Yes/No cell from an uploaded sheet. Blank = <paramref name="defaultValue"/>; anything other than yes/y/true/1 or no/n/false/0 is rejected so a typo is never silently treated as a default.</summary>
        public static bool TryParseYesNo(string? raw, bool defaultValue, out bool value)
        {
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text)) { value = defaultValue; return true; }
            switch (text.ToLowerInvariant())
            {
                case "yes": case "y": case "true": case "1": value = true; return true;
                case "no": case "n": case "false": case "0": value = false; return true;
                default: value = defaultValue; return false;
            }
        }
    }
}
