-- Synthetic laboratory report model. No production data.
-- Schemas: lab holds rows, rpt holds the read contract.

CREATE SCHEMA lab;
GO
CREATE SCHEMA rpt;
GO

CREATE TABLE lab.Tenant (
    TenantId nvarchar(64) NOT NULL CONSTRAINT PK_Tenant PRIMARY KEY,
    Name nvarchar(200) NOT NULL
);

CREATE TABLE lab.Principal (
    PrincipalId nvarchar(64) NOT NULL CONSTRAINT PK_Principal PRIMARY KEY,
    TenantId nvarchar(64) NOT NULL,
    DisplayName nvarchar(200) NOT NULL,
    CONSTRAINT FK_Principal_Tenant FOREIGN KEY (TenantId) REFERENCES lab.Tenant (TenantId)
);

CREATE TABLE lab.Report (
    TenantId nvarchar(64) NOT NULL,
    ReportId nvarchar(64) NOT NULL,
    ReportNumber nvarchar(64) NOT NULL,
    SchemaVersion nvarchar(16) NOT NULL,
    Title nvarchar(300) NOT NULL,
    ReportDate date NOT NULL,
    Matrix nvarchar(120) NOT NULL,
    Commodity nvarchar(120) NOT NULL,
    ClientName nvarchar(200) NOT NULL,
    ClientReference nvarchar(120) NULL,
    Origin nvarchar(200) NULL,
    OrderedBy nvarchar(200) NULL,
    SubmittedBy nvarchar(200) NULL,
    SourceFeed nvarchar(80) NOT NULL,
    SourceRevision nvarchar(64) NOT NULL,
    CONSTRAINT PK_Report PRIMARY KEY (TenantId, ReportId),
    CONSTRAINT FK_Report_Tenant FOREIGN KEY (TenantId) REFERENCES lab.Tenant (TenantId)
);

CREATE TABLE lab.ReportAccess (
    TenantId nvarchar(64) NOT NULL,
    ReportId nvarchar(64) NOT NULL,
    PrincipalId nvarchar(64) NOT NULL,
    AccessLevel nvarchar(32) NOT NULL,
    CONSTRAINT PK_ReportAccess PRIMARY KEY (TenantId, ReportId, PrincipalId),
    CONSTRAINT FK_ReportAccess_Report FOREIGN KEY (TenantId, ReportId) REFERENCES lab.Report (TenantId, ReportId),
    CONSTRAINT FK_ReportAccess_Principal FOREIGN KEY (PrincipalId) REFERENCES lab.Principal (PrincipalId),
    CONSTRAINT CK_ReportAccess_Level CHECK (AccessLevel IN (N'reader', N'admin'))
);

CREATE TABLE lab.Sample (
    TenantId nvarchar(64) NOT NULL,
    SampleId nvarchar(64) NOT NULL,
    ReportId nvarchar(64) NOT NULL,
    SampleCode nvarchar(64) NOT NULL,
    Ordinal int NOT NULL,
    Description nvarchar(max) NOT NULL,
    Matrix nvarchar(120) NOT NULL,
    Commodity nvarchar(120) NOT NULL,
    SamplingDate date NULL,
    ReceivedDate date NULL,
    ReceptionTemperature nvarchar(80) NULL,
    BabyFood bit NOT NULL CONSTRAINT DF_Sample_BabyFood DEFAULT (0),
    Organic bit NOT NULL CONSTRAINT DF_Sample_Organic DEFAULT (0),
    CONSTRAINT PK_Sample PRIMARY KEY (TenantId, SampleId),
    CONSTRAINT UQ_Sample_Ordinal UNIQUE (TenantId, ReportId, Ordinal),
    CONSTRAINT FK_Sample_Report FOREIGN KEY (TenantId, ReportId) REFERENCES lab.Report (TenantId, ReportId)
);

CREATE TABLE lab.Analyte (
    AnalyteCode nvarchar(64) NOT NULL CONSTRAINT PK_Analyte PRIMARY KEY,
    AnalyteName nvarchar(200) NOT NULL,
    AnalyteGroup nvarchar(64) NOT NULL
);

CREATE TABLE lab.MethodReference (
    MethodCode nvarchar(64) NOT NULL CONSTRAINT PK_Method PRIMARY KEY,
    MethodName nvarchar(300) NOT NULL,
    Summary nvarchar(2000) NOT NULL,
    ReferenceCitation nvarchar(500) NOT NULL,
    VersionLabel nvarchar(64) NOT NULL
);

CREATE TABLE lab.Result (
    TenantId nvarchar(64) NOT NULL,
    ResultId nvarchar(64) NOT NULL,
    SampleId nvarchar(64) NOT NULL,
    AnalyteCode nvarchar(64) NOT NULL,
    Ordinal int NOT NULL,
    ResultKind nvarchar(32) NOT NULL,
    NumericValue decimal(18, 6) NULL,
    Qualifier nvarchar(16) NULL,
    Lod decimal(18, 6) NULL,
    Loq decimal(18, 6) NULL,
    Unit nvarchar(32) NULL,
    MethodCode nvarchar(64) NULL,
    CONSTRAINT PK_Result PRIMARY KEY (TenantId, ResultId),
    CONSTRAINT UQ_Result_Ordinal UNIQUE (TenantId, SampleId, Ordinal),
    CONSTRAINT UQ_Result_Analyte UNIQUE (TenantId, SampleId, AnalyteCode),
    CONSTRAINT FK_Result_Sample FOREIGN KEY (TenantId, SampleId) REFERENCES lab.Sample (TenantId, SampleId),
    CONSTRAINT FK_Result_Analyte FOREIGN KEY (AnalyteCode) REFERENCES lab.Analyte (AnalyteCode),
    CONSTRAINT FK_Result_Method FOREIGN KEY (MethodCode) REFERENCES lab.MethodReference (MethodCode),
    CONSTRAINT CK_Result_Kind CHECK (ResultKind IN (N'numeric', N'belowLoq', N'notDetected', N'notTested', N'missing'))
);

CREATE TABLE lab.MrlLimit (
    MrlId nvarchar(64) NOT NULL CONSTRAINT PK_Mrl PRIMARY KEY,
    AnalyteCode nvarchar(64) NOT NULL,
    Jurisdiction nvarchar(80) NOT NULL,
    Matrix nvarchar(120) NOT NULL,
    Commodity nvarchar(120) NOT NULL,
    Unit nvarchar(32) NOT NULL,
    LimitValue decimal(18, 6) NOT NULL,
    EffectiveFrom date NOT NULL,
    EffectiveTo date NULL,
    SourceName nvarchar(200) NOT NULL,
    VersionLabel nvarchar(64) NOT NULL,
    Fictional bit NOT NULL CONSTRAINT DF_Mrl_Fictional DEFAULT (1),
    CONSTRAINT FK_Mrl_Analyte FOREIGN KEY (AnalyteCode) REFERENCES lab.Analyte (AnalyteCode),
    CONSTRAINT CK_Mrl_Fictional CHECK (Fictional = 1)
);

CREATE TABLE lab.TextCatalog (
    TextKey nvarchar(120) NOT NULL,
    LanguageCode nvarchar(16) NOT NULL,
    VersionLabel nvarchar(32) NOT NULL,
    Scope nvarchar(32) NOT NULL,
    Category nvarchar(32) NOT NULL,
    EffectiveFrom date NOT NULL,
    EffectiveTo date NULL,
    ApprovalStatus nvarchar(32) NOT NULL,
    Body nvarchar(max) NOT NULL,
    CONSTRAINT PK_TextCatalog PRIMARY KEY (TextKey, LanguageCode, VersionLabel),
    CONSTRAINT CK_Text_Scope CHECK (Scope IN (N'report', N'sample', N'result', N'method', N'section')),
    CONSTRAINT CK_Text_Status CHECK (ApprovalStatus IN (N'approved', N'draft'))
);

CREATE TABLE lab.TextBinding (
    TenantId nvarchar(64) NOT NULL,
    BindingId nvarchar(64) NOT NULL,
    ReportId nvarchar(64) NOT NULL,
    TextKey nvarchar(120) NOT NULL,
    LanguageCode nvarchar(16) NOT NULL,
    Scope nvarchar(32) NOT NULL,
    SampleId nvarchar(64) NULL,
    ResultId nvarchar(64) NULL,
    MethodCode nvarchar(64) NULL,
    CONSTRAINT PK_TextBinding PRIMARY KEY (TenantId, BindingId),
    CONSTRAINT FK_TextBinding_Report FOREIGN KEY (TenantId, ReportId) REFERENCES lab.Report (TenantId, ReportId)
);

CREATE TABLE lab.Signatory (
    SignatoryId nvarchar(64) NOT NULL CONSTRAINT PK_Signatory PRIMARY KEY,
    DisplayName nvarchar(200) NOT NULL,
    RoleTitle nvarchar(200) NOT NULL,
    Statement nvarchar(1000) NOT NULL,
    AssetKey nvarchar(64) NOT NULL
);

CREATE INDEX IX_Sample_Report ON lab.Sample (TenantId, ReportId, Ordinal);
CREATE INDEX IX_Result_Sample ON lab.Result (TenantId, SampleId, Ordinal);
CREATE INDEX IX_Binding_Report ON lab.TextBinding (TenantId, ReportId, BindingId);
CREATE INDEX IX_Mrl_Match ON lab.MrlLimit (AnalyteCode, Jurisdiction, Matrix, Commodity, EffectiveFrom);
