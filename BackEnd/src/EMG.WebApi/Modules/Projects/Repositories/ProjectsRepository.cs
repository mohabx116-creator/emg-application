using EMG.Api.Modules.Projects.Dtos;

namespace EMG.Api.Modules.Projects.Repositories;

public sealed class ProjectsRepository : IProjectsRepository
{
    private static readonly List<ProjectSummary> Projects =
    [
        new(1, "191", "PRJ-0191", "عميل مشروع 191", "العاشر من رمضان", "نشط", "م. يوسف الشاذلي", 3, 6399398m, 3200702m),
        new(2, "للصناعات ق315 منطقة الصناعية ال710 اسلام عدوي", "PRJ-0315", "عميل مشروع ق315", "المنطقة الصناعية 710", "نشط", "م. أحمد حنفي", 8, 10951183.45m, 9710400m),
        new(3, "حبيب", "PRJ-HABIB", "حبيب", "المنطقة الصناعية", "تحت المراجعة", "م. أمجد عسكر", 2, 3267106.3m, 3067680m),
        new(4, "ولاء محمد توفيق ( اليسر للصناعه)", "PRJ-0132", "ولاء محمد توفيق", "أكتوبر الجديدة", "نشط", "م. إبراهيم", 4, 5972390m, 8687350m)
    ];

    public Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(int? userId, bool isAdmin, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectSummary>>(Projects.ToArray());

    public Task<ProjectSummary?> GetProjectAsync(int id, int? userId, bool isAdmin, CancellationToken cancellationToken) =>
        Task.FromResult(Projects.FirstOrDefault(project => project.Id == id));

    public Task<ProjectSummary> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = new ProjectSummary(
            Projects.Count == 0 ? 1 : Projects.Max(project => project.Id) + 1,
            request.Name.Trim(), request.Code.Trim(), request.ClientName?.Trim() ?? string.Empty,
            request.Location?.Trim() ?? string.Empty, string.IsNullOrWhiteSpace(request.Status) ? "نشط" : request.Status.Trim(),
            request.EngineerName?.Trim() ?? string.Empty, 0, 0, 0);
        Projects.Add(project);
        return Task.FromResult(project);
    }

    public Task<ProjectSummary?> UpdateAsync(int id, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var index = Projects.FindIndex(project => project.Id == id);
        if (index < 0) return Task.FromResult<ProjectSummary?>(null);
        var current = Projects[index];
        var updated = current with
        {
            Name = request.Name.Trim(), Code = request.Code.Trim(), ClientName = request.ClientName?.Trim() ?? string.Empty,
            Location = request.Location?.Trim() ?? string.Empty, Status = string.IsNullOrWhiteSpace(request.Status) ? "نشط" : request.Status.Trim(),
            EngineerName = request.EngineerName?.Trim() ?? string.Empty
        };
        Projects[index] = updated;
        return Task.FromResult<ProjectSummary?>(updated);
    }

    public Task<ProjectSummary?> DecideImportAsync(int id, bool approve, string? notes, int? userId, CancellationToken cancellationToken)
    {
        var index = Projects.FindIndex(project => project.Id == id && project.Status == "تحت المراجعة");
        if (index < 0) return Task.FromResult<ProjectSummary?>(null);

        var updated = Projects[index] with { Status = approve ? "نشط" : "مرفوض" };
        Projects[index] = updated;
        return Task.FromResult<ProjectSummary?>(updated);
    }
}
