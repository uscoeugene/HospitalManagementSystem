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

        public PatientService(HmsDbContext db)
        {
            _db = db;
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
                p.DateOfBirth.Date == request.DateOfBirth.Date);

            if (nameDobExists) throw new InvalidOperationException("A patient with the same name and date of birth already exists");

            var patient = new HMS.API.Domain.Patient.Patient
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender,
                Phone = request.Phone,
                Email = request.Email,
                MedicalRecordNumber = request.MedicalRecordNumber ?? Guid.NewGuid().ToString("N")
            };

            _db.Patients.Add(patient);
            await _db.SaveChangesAsync();

            return new PatientResponse
            {
                Id = patient.Id,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                Phone = patient.Phone,
                Email = patient.Email,
                MedicalRecordNumber = patient.MedicalRecordNumber
            };
        }

        public async Task<PatientResponse?> GetPatientAsync(Guid id)
        {
            var patient = await _db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
            if (patient == null) return null;
            return new PatientResponse
            {
                Id = patient.Id,
                FirstName = patient.FirstName,
                LastName = patient.LastName,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                Phone = patient.Phone,
                Email = patient.Email,
                MedicalRecordNumber = patient.MedicalRecordNumber
            };
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
                q = q.Where(p => EF.Functions.Like(p.FirstName, $"%{search}%") || EF.Functions.Like(p.LastName, $"%{search}%") || p.MedicalRecordNumber == search);
            }

            var total = await q.CountAsync();
            var items = await q.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new PatientResponse
                {
                    Id = p.Id,
                    FirstName = p.FirstName,
                    LastName = p.LastName,
                    DateOfBirth = p.DateOfBirth,
                    Gender = p.Gender,
                    Phone = p.Phone,
                    Email = p.Email,
                    MedicalRecordNumber = p.MedicalRecordNumber
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
            // try parse date in query
            DateTimeOffset? parsedDate = null;
            if (DateTimeOffset.TryParse(query, out var pd)) parsedDate = pd;

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
                    var days = Math.Abs((p.DateOfBirth.Date - parsedDate.Value.Date).TotalDays);
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
    }
}