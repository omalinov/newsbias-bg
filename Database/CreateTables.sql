USE NewsPlatformDb;
GO

CREATE TABLE NewsSource (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Url NVARCHAR(500) NOT NULL,
    Category NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    PoliticalLeaning NVARCHAR(20) NOT NULL DEFAULT 'unknown', -- 'pro-eu', 'pro-russia', 'neutral', 'unknown'
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE Article (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SourceId INT NOT NULL,
    Title NVARCHAR(500) NOT NULL,
    Summary NVARCHAR(MAX) NULL,
    Content NVARCHAR(MAX) NULL,
    Url NVARCHAR(500) NULL,
    PublishedAt DATETIME2 NOT NULL,
    Topic NVARCHAR(100) NULL,
    Tone NVARCHAR(20) NOT NULL DEFAULT 'unknown', -- 'positive', 'negative', 'neutral', 'sensationalist', 'unknown'
	FactualityLevel NVARCHAR(20) NOT NULL DEFAULT 'unknown', -- 'factual', 'mixed', 'opinion', 'propaganda', 'tabloid', 'unknown'

    CONSTRAINT FK_Article_Source FOREIGN KEY (SourceId)
        REFERENCES NewsSource(Id)
        ON DELETE CASCADE
);

CREATE TABLE AppUser (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Email NVARCHAR(200) NOT NULL UNIQUE,
	PoliticalPreference NVARCHAR(20) NOT NULL DEFAULT 'none' -- 'none', 'pro-eu', 'pro-russia', 'neutral', 'unknown'
);

CREATE TABLE UserPreference (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Topic NVARCHAR(100) NULL,

    CONSTRAINT FK_UserPreference_User FOREIGN KEY (UserId)
        REFERENCES AppUser(Id)
        ON DELETE CASCADE
);