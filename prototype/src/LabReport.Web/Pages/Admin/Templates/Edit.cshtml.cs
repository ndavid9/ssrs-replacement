using LabReport.Core.Models;
using LabReport.Core.Services;
using LabReport.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LabReport.Web.Pages.Admin.Templates;

public sealed class EditModel : PageModel
{
    private readonly FileTemplateStore _templates;
    private readonly ICatalogSource _catalogs;

    public EditModel(FileTemplateStore templates, ICatalogSource catalogs)
    {
        _templates = templates;
        _catalogs = catalogs;
    }

    public TemplateDefinition? Template { get; private set; }
    public IReadOnlyList<SignatoryBlock> Signatories { get; private set; } = [];
    public string? Message { get; private set; }

    public IActionResult OnGet(string templateId)
    {
        Template = _templates.Find(templateId);
        Signatories = _catalogs.Signatories();
        return Template is null ? NotFound() : Page();
    }

    public IActionResult OnPostAdd(string templateId, string addType)
    {
        var template = _templates.Require(templateId);
        try
        {
            _templates.Save(TemplateEditor.AddSection(template, addType));
            Message = $"Added {addType}.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return Redirect($"/admin/templates/{templateId}");
    }

    public IActionResult OnPostRemove(string templateId, string instanceId)
    {
        _templates.Save(TemplateEditor.RemoveSection(_templates.Require(templateId), instanceId));
        return Redirect($"/admin/templates/{templateId}");
    }

    public IActionResult OnPostMove(string templateId, string instanceId, int direction)
    {
        _templates.Save(TemplateEditor.Move(_templates.Require(templateId), instanceId, direction));
        return Redirect($"/admin/templates/{templateId}");
    }

    public IActionResult OnPostSave(
        string templateId,
        string name,
        int samplesPerPage,
        int resultRowsPerChunk,
        string paper,
        string orientation,
        int marginTopMm,
        int marginBottomMm,
        int marginSideMm,
        string signatoryId,
        string jurisdiction,
        string[]? enabled,
        string[]? column,
        string[]? subsection)
    {
        var current = _templates.Require(templateId);
        var enabledSet = new HashSet<string>(enabled ?? [], StringComparer.Ordinal);
        var columnSet = new HashSet<string>(column ?? [], StringComparer.Ordinal);
        var subsectionSet = new HashSet<string>(subsection ?? [], StringComparer.Ordinal);
        var edits = current.Sections.Select(section => new SectionEdit
        {
            InstanceId = section.InstanceId,
            Title = Request.Form[$"title-{section.InstanceId}"].ToString(),
            Enabled = enabledSet.Contains(section.InstanceId),
            Columns = section.Columns.Select(item => new ColumnConfig
            {
                Key = item.Key,
                Label = item.Label,
                Visible = columnSet.Contains(section.InstanceId + "|" + item.Key)
            }).ToList(),
            Subsections = section.Subsections.Select(item => item with
            {
                Enabled = subsectionSet.Contains(section.InstanceId + "|" + item.Id),
                Title = string.IsNullOrWhiteSpace(Request.Form[$"sub-{section.InstanceId}-{item.Id}"])
                    ? item.Title
                    : Request.Form[$"sub-{section.InstanceId}-{item.Id}"].ToString()
            }).ToList()
        }).ToList();
        try
        {
            var next = TemplateEditor.Apply(current, new TemplateEdit
            {
                Name = name,
                SamplesPerPage = samplesPerPage,
                ResultRowsPerChunk = resultRowsPerChunk,
                Paper = paper,
                Orientation = orientation,
                MarginTopMm = marginTopMm,
                MarginBottomMm = marginBottomMm,
                MarginSideMm = marginSideMm,
                SignatoryId = signatoryId,
                Jurisdiction = jurisdiction,
                Sections = edits
            });
            if (_catalogs.Signatory(next.SignatoryId) is null)
            {
                throw new InvalidOperationException("Choose a signatory id from the catalog.");
            }

            _templates.Save(next);
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return Redirect($"/admin/templates/{templateId}");
    }
}
