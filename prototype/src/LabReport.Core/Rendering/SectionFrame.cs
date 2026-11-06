using LabReport.Core.Models;

namespace LabReport.Core.Rendering;

public sealed record SectionFrame(RenderPlan Plan, SectionConfig Section, SampleView? Sample);
