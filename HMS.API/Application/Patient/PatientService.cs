using System;
using System.Linq;
using System.Threading.Tasks;
using HMS.API.Application.Patient.DTOs;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HMS.API.Application.Common;
using System.Text.RegularExpressions;

namespace HMS.API.Application.Patient
{
    public class PatientService : IPatientService
    {
        private readonly HmsDbContext _db;
        private readonly Infrastructure.Auth.AuthDbContext _authDb;

        public PatientService(HmsDbContext db, Infrastructure.Auth.AuthDbContext authDb)
        {
            _db = db;
            _authDb = authDb;
        }

        public async Task<PatientResponse> UpdatePatientAsync(Guid id, RegisterPatientRequest request)
        {
            var patient = await _db.Patients.SingleOrDefaultAsync(p => p.Id == id);
            if (patient == null) throw new InvalidOperationException("Patient not found");

            // update editable fields
            patient.FirstName = request.FirstName;
            patient.MiddleName = request.MiddleName;
            patient.LastName = request.LastName;
            patient.DateOfBirth = request.DateOfBirth;
            patient.Gender = request.Gender;
            patient.Phone = request.Phone;
            patient.AlternatePhone = request.AlternatePhone;
            patient.Email = request.Email;
            patient.MedicalRecordNumber = request.MedicalRecordNumber ?? patient.MedicalRecordNumber;

            // optional extended fields
            patient.AddressLine1 = request.AddressLine1;
            patient.AddressLine2 = request.AddressLine2;
            patient.City = request.City;
            patient.State = request.State;
            patient.PostalCode = request.PostalCode;
            patient.Country = request.Country;
            patient.MaritalStatus = request.MaritalStatus;
            patient.Nationality = request.Nationality;
            patient.NationalIdNumber = request.NationalIdNumber;
            patient.BloodGroup = request.BloodGroup;
            patient.Genotype = request.Genotype;
            patient.EmergencyContactName = request.EmergencyContactName;
            patient.EmergencyContactRelationship = request.EmergencyContactRelationship;
            patient.EmergencyContactPhone = request.EmergencyContactPhone;
            patient.InsuranceProvider = request.InsuranceProvider;
            patient.InsuranceNumber = request.InsuranceNumber;
            patient.Occupation = request.Occupation;
            patient.PhotoUrl = request.PhotoUrl;
            patient.IsActive = request.IsActive;

            await _db.SaveChangesAsync();

            return MapToResponse(patient);
        }

        public async Task<PatientResponse> RegisterPatientAsync(RegisterPatientRequest request)
        {
            // Duplicate detection: medical record number uniqueness first
            if (!string.IsNullOrWhiteSpace(request.MedicalRecordNumber))
            {
                var existsMrn = await _db.Patients.AnyAsync(p => p.MedicalRecordNumber == request.MedicalRecordNumber && !p.IsDeleted);
                if (existsMrn) throw new InvalidOperationException("MedicalRecordNumber already exists");
            }

            // Fuzzy duplicate detection: same name + DOB
            var nameDobExists = await _db.Patients.AnyAsync(p => !p.IsDeleted &&
                EF.Functions.Like(p.FirstName, request.FirstName) &&
                EF.Functions.Like(p.LastName, request.LastName) &&
                p.DateOfBirth == request.DateOfBirth);

            if (nameDobExists) throw new InvalidOperationException("A patient with the same name and date of birth already exists");

            var patient = new HMS.API.Domain.Patient.Patient
            {
                FirstName = request.FirstName,
                MiddleName = request.MiddleName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                Phone = request.Phone,
                AlternatePhone = request.AlternatePhone,
                Email = request.Email,
                MedicalRecordNumber = request.MedicalRecordNumber ?? await GenerateMedicalRecordNumberAsync(),
                MaritalStatus = request.MaritalStatus,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode,
                Country = request.Country,
                Nationality = request.Nationality,
                NationalIdNumber = request.NationalIdNumber,
                BloodGroup = request.BloodGroup,
                Genotype = request.Genotype,
                EmergencyContactName = request.EmergencyContactName,
                EmergencyContactRelationship = request.EmergencyContactRelationship,
                EmergencyContactPhone = request.EmergencyContactPhone,
                InsuranceProvider = request.InsuranceProvider,
                InsuranceNumber = request.InsuranceNumber,
                Occupation = request.Occupation,
                PhotoUrl = request.PhotoUrl,
                IsActive = request.IsActive
            };

            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            return MapToResponse(patient);
        }

        private async Task<string> GenerateMedicalRecordNumberAsync()
        {
            // Tenant-specific prefix from AuthDbContext if available
            string prefix = "HMS";
            Guid? tid = CurrentTenantAccessor.CurrentTenantId;
            if (tid.HasValue)
            {
                try
                {
                    var tenant = await _authDb.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tid.Value);
                    if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Code)) prefix = tenant.Code.ToUpperInvariant();
                }
                catch { }
            }

            // Search existing MRNs for this tenant (or null tenant for central) with the same prefix
            var q = _db.Patients.AsNoTracking().Where(p => !p.IsDeleted && p.MedicalRecordNumber.StartsWith(prefix));
            if (tid.HasValue)
            {
                q = q.Where(p => p.TenantId == tid.Value);
            }
            long max = 1000000; // start baseline so first generated becomes 1000001

            //var list = await q.Select(p => p.MedicalRecordNumber).ToListAsync();

            //foreach (var mrn in list)
            //{
            //    if (mrn.Length <= prefix.Length) continue;
            //    var suffix = mrn.Substring(prefix.Length);
            //    if (long.TryParse(suffix, out var n))
            //    {
            //        if (n > max) max = n;
            //    }
            //}
            var maxMrn = await q
                                .OrderByDescending(p => p.MedicalRecordNumber)
                                .Select(p => p.MedicalRecordNumber)
                                .FirstOrDefaultAsync();

           
            if (!string.IsNullOrWhiteSpace(maxMrn) &&
     maxMrn.Length > prefix.Length)
            {
                var suffix = maxMrn.Substring(prefix.Length);

                if (long.TryParse(suffix, out var n))
                {
                    if (n > max) max = n;
                }
            }
            var next = max + 1;
            // format with zero padding to 7 digits
            var num = next.ToString().PadLeft(7, '0');
            return prefix + num;
        }

        public async Task<PatientResponse?> GetPatientAsync(Guid id)
        {
            var patient = await _db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
            if (patient == null) return null;
            return MapToResponse(patient);
        }

        public async Task<VisitResponse> AddVisitAsync(Guid patientId, AddVisitRequest request)
        {
            var patient = await _db.Patients.SingleOrDefaultAsync(p => p.Id == patientId);
            if (patient == null) throw new InvalidOperationException("Patient not found");

            var visit = new HMS.API.Domain.Patient.Visit
            {
                Patient = patient,
                VisitAt = request.VisitAt,
                VisitType = request.VisitType,
                Notes = request.Notes ?? string.Empty
            };

            _db.Visits.Add(visit);
            await _db.SaveChangesAsync();

            return new VisitResponse
            {
                Id = visit.Id,
                VisitAt = visit.VisitAt,
                VisitType = visit.VisitType,
                Notes = visit.Notes
            };
        }

        public async Task<PagedResult<PatientResponse>> ListPatientsAsync(string? search, int page = 1, int pageSize = 20)
        {
            var q = _db.Patients.AsNoTracking().Where(p => !p.IsDeleted);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                q = q.Where(p =>
                    EF.Functions.Like(p.FirstName, $"%{search}%") ||
                    EF.Functions.Like(p.LastName, $"%{search}%") ||
                    EF.Functions.Like(p.MedicalRecordNumber, $"%{search}%"));
            }

            var total = await q.CountAsync();
            var items = await q.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Skip((page - 1) * pageSize).Take(pageSize)
              .Select(p => new PatientResponse
              {
                  Id = p.Id,
                  FirstName = p.FirstName,
                  MiddleName = p.MiddleName,
                  LastName = p.LastName,
                  DateOfBirth = p.DateOfBirth,
                  Gender = p.Gender,
                  Phone = p.Phone,
                  AlternatePhone = p.AlternatePhone,
                  Email = p.Email,
                  MedicalRecordNumber = p.MedicalRecordNumber,

                  AddressLine1 = p.AddressLine1,
                  AddressLine2 = p.AddressLine2,
                  City = p.City,
                  State = p.State,
                  PostalCode = p.PostalCode,
                  Country = p.Country,

                  MaritalStatus = p.MaritalStatus,

                  Nationality = p.Nationality,
                  NationalIdNumber = p.NationalIdNumber,

                  BloodGroup = p.BloodGroup,
                  Genotype = p.Genotype,

                  EmergencyContactName = p.EmergencyContactName,
                  EmergencyContactRelationship = p.EmergencyContactRelationship,
                  EmergencyContactPhone = p.EmergencyContactPhone,

                  InsuranceProvider = p.InsuranceProvider,
                  InsuranceNumber = p.InsuranceNumber,

                  Occupation = p.Occupation,
                  PhotoUrl = p.PhotoUrl,

                  IsActive = p.IsActive
              }).ToListAsync();

            return new PagedResult<PatientResponse>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<DuplicateCandidateDto[]> FindPossibleDuplicatesAsync(string query, double threshold = 0.75, int dobToleranceDays = 365, int mrnPrefixLength = 4)
        {
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<DuplicateCandidateDto>();

            query = query.Trim();
            // try parse date in query (as DateOnly)
            DateOnly? parsedDate = null;
            if (DateOnly.TryParse(query, out var pd)) parsedDate = pd;
            else if (DateTimeOffset.TryParse(query, out var pd2)) parsedDate = DateOnly.FromDateTime(pd2.DateTime);

            // try find 4-digit year
            var yearMatch = Regex.Match(query, "(19|20)\\d{2}");
            int? yearInQuery = null;
            if (yearMatch.Success && int.TryParse(yearMatch.Value, out var y)) yearInQuery = y;

            var candidates = await _db.Patients.AsNoTracking().Where(p => !p.IsDeleted).ToListAsync();

            var results = candidates.Select(p =>
            {
                var name = (p.FirstName + " " + p.LastName).Trim();
                var nameSim = StringSimilarity.Similarity(name, query);

                double dobBonus = 0.0;
                if (parsedDate.HasValue)
                {
                    var days = Math.Abs(p.DateOfBirth.DayNumber - parsedDate.Value.DayNumber);
                    if (days <= dobToleranceDays)
                    {
                        dobBonus = 0.25 * (1.0 - days / (double)dobToleranceDays); // up to 0.25
                    }
                }
                else if (yearInQuery.HasValue)
                {
                    if (p.DateOfBirth.Year == yearInQuery.Value) dobBonus = 0.15;
                    else if (Math.Abs(p.DateOfBirth.Year - yearInQuery.Value) <= 1) dobBonus = 0.07;
                }

                double mrnBonus = 0.0;
                if (!string.IsNullOrWhiteSpace(p.MedicalRecordNumber))
                {
                    var mrn = p.MedicalRecordNumber;
                    var prefixLen = Math.Min(mrnPrefixLength, Math.Min(mrn.Length, query.Length));
                    if (prefixLen > 0)
                    {
                        var mrnPrefix = mrn.Substring(0, prefixLen);
                        if (query.StartsWith(mrnPrefix, StringComparison.OrdinalIgnoreCase)) mrnBonus = 0.3;
                        else if (query.IndexOf(mrnPrefix, StringComparison.OrdinalIgnoreCase) >= 0) mrnBonus = 0.15;
                    }
                }

                var combined = Math.Min(1.0, nameSim + dobBonus + mrnBonus);

                return new DuplicateCandidateDto
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    DateOfBirth = p.DateOfBirth,
                    MedicalRecordNumber = p.MedicalRecordNumber,
                    Similarity = combined
                };
            }).Where(d => d.Similarity >= threshold).OrderByDescending(d => d.Similarity).ToArray();

            return results;
        }

        public async Task<MergePatientsResult> MergePatientsAsync(MergePatientsRequest request)
        {
            var target = await _db.Patients.Include(p => p.Visits).SingleOrDefaultAsync(p => p.Id == request.TargetPatientId);
            var source = await _db.Patients.Include(p => p.Visits).SingleOrDefaultAsync(p => p.Id == request.SourcePatientId);
            if (target == null || source == null) throw new InvalidOperationException("Patient not found");

            // Move visits
            foreach (var v in source.Visits.ToList())
            {
                v.PatientId = target.Id;
                v.Patient = target;
                target.Visits.Add(v);
            }

            // Merge fields
            if (request.PreferSourceName)
            {
                if (!string.IsNullOrWhiteSpace(source.FirstName)) target.FirstName = source.FirstName;
                if (!string.IsNullOrWhiteSpace(source.LastName)) target.LastName = source.LastName;
            }

            if (request.PreferSourceContact)
            {
                if (!string.IsNullOrWhiteSpace(source.Phone)) target.Phone = source.Phone;
                if (!string.IsNullOrWhiteSpace(source.Email)) target.Email = source.Email;
            }

            // ensure target marked unsynced for later sync
            target.IsSynced = false;
            target.SyncVersion = Math.Max(target.SyncVersion, source.SyncVersion);

            // soft-delete source
            source.SoftDelete();

            await _db.SaveChangesAsync();

            return new MergePatientsResult
            {
                TargetPatientId = target.Id,
                MergedSourceIds = new[] { source.Id }
            };
        }

        private static PatientResponse MapToResponse(HMS.API.Domain.Patient.Patient patient)
        {
            return new PatientResponse
            {
                Id = patient.Id,
                FirstName = patient.FirstName,
                MiddleName = patient.MiddleName,
                LastName = patient.LastName,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                MaritalStatus = patient.MaritalStatus,
                Phone = patient.Phone,
                AlternatePhone = patient.AlternatePhone,
                Email = patient.Email,
                MedicalRecordNumber = patient.MedicalRecordNumber,

                AddressLine1 = patient.AddressLine1,
                AddressLine2 = patient.AddressLine2,
                City = patient.City,
                State = patient.State,
                PostalCode = patient.PostalCode,
                Country = patient.Country,

                Nationality = patient.Nationality,
                NationalIdNumber = patient.NationalIdNumber,

                BloodGroup = patient.BloodGroup,
                Genotype = patient.Genotype,

                EmergencyContactName = patient.EmergencyContactName,
                EmergencyContactRelationship = patient.EmergencyContactRelationship,
                EmergencyContactPhone = patient.EmergencyContactPhone,

                InsuranceProvider = patient.InsuranceProvider,
                InsuranceNumber = patient.InsuranceNumber,

                Occupation = patient.Occupation,
                PhotoUrl = patient.PhotoUrl,

                IsActive = patient.IsActive
            };
        }
    }
}