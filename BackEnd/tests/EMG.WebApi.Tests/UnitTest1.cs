using EMG.Api.Modules.Projects.Controllers;
using EMG.Api.Modules.Projects.Dtos;
using EMG.Api.Modules.Projects.Repositories;
using EMG.Api.Modules.Projects.Services;
using Microsoft.AspNetCore.Mvc;
using EMG.Api.Modules.Imports.Services;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using System.Text;

namespace EMG.WebApi.Tests;

public sealed class ProjectsTests
{
    [Fact]
    public async Task GetProjects_ReturnsPilotProjects()
    {
        var service = CreateService();
        var projects = await service.GetProjectsAsync(null, true, CancellationToken.None);
        Assert.NotEmpty(projects);
        Assert.Contains(projects, project => project.Code == "PRJ-0191");
    }

    [Fact]
    public async Task CreateProject_ReturnsCreatedProject()
    {
        var controller = new ProjectsController(CreateService());
        var request = new CreateProjectRequest("مشروع اختبار الوحدة", "PRJ-UNIT-001", "عميل", "مالك", "دافع", "الموقع", "نشط", "م. اختبار", 10);
        var result = await controller.CreateProject(request, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var project = Assert.IsType<ProjectSummary>(created.Value);
        Assert.Equal("PRJ-UNIT-001", project.Code);
    }

    [Fact]
    public async Task CreateProject_RejectsInvalidOfficeSharePercentage()
    {
        var controller = new ProjectsController(CreateService());
        var request = new CreateProjectRequest("مشروع", "PRJ-INVALID", null, null, null, null, null, null, 101);
        var result = await controller.CreateProject(request, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetProject_ReturnsNotFoundForUnknownId()
    {
        var controller = new ProjectsController(CreateService());
        var result = await controller.GetProject(999999, CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task WorkbookPreview_ClassifiesKnownAndUnknownSheetsWithoutPersisting()
    {
        await using var stream = CreateWorkbook("اعمال الحفر", "Sheet1", "اعمال توريد الحديد اولاد شرف");
        var file = new FormFile(stream, 0, stream.Length, "file", "sample.xlsx");
        var preview = await new WorkbookImportService().PreviewAsync(file, CancellationToken.None);

        Assert.True(preview.IsValid);
        Assert.True(preview.RequiresReview);
        Assert.Equal("الحفر", preview.Sheets[0].Classification);
        Assert.Equal("صفحة غير معنونة", preview.Sheets[1].Classification);
        Assert.Contains(preview.Warnings, warning => warning.Contains("Sheet1", StringComparison.Ordinal));
    }

    private static MemoryStream CreateWorkbook(params string[] sheetNames)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("xl/workbook.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>{string.Join(string.Empty, sheetNames.Select((name, index) => $"<sheet name=\"{name}\" sheetId=\"{index + 1}\" r:id=\"rId{index + 1}\" />"))}</sheets></workbook>");
        }
        stream.Position = 0;
        return stream;
    }

    private static IProjectsService CreateService() => new ProjectsService(new ProjectsRepository());
}
