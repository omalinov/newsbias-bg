USE NewsPlatformDb;
GO

-- 1) Sources
INSERT INTO NewsSource (Name, Url, Category, PoliticalLeaning)
VALUES
('Capital.bg', 'https://www.capital.bg', 'economy', 'pro-eu'),
('Pogled.info', 'https://pogled.info', 'politics', 'pro-russia'),
('BNT', 'https://www.bnt.bg', 'general', 'neutral');

-- Check
SELECT * FROM NewsSource;
GO

-- 2) Articles
INSERT INTO Article (
    SourceId,
    Title,
    Summary,
    Content,
    Url,
    PublishedAt,
    Topic,
    Tone,
    FactualityLevel
)
VALUES
(1, 'BG economy grows', 'Short summary 1', NULL,
 'https://www.capital.bg/article1', '2025-01-01T10:00:00', 'economy', 'positive', 'factual'),
(1, 'Corruption scandal', 'Short summary 2', NULL,
 'https://www.capital.bg/article2', '2025-01-02T11:00:00', 'politics', 'negative', 'mixed'),
(2, 'NATO is bad', 'Short summary 3', NULL,
 'https://pogled.info/article1', '2025-01-03T12:00:00', 'geopolitics', 'sensationalist', 'propaganda'),
(3, 'Weather and sports', 'Short summary 4', NULL,
 'https://www.bnt.bg/article1', '2025-01-04T13:00:00', 'general', 'neutral', 'factual');

-- Check
SELECT * FROM Article;
GO

-- 3) Users
INSERT INTO AppUser (Name, Email, PoliticalPreference)
VALUES
('Ivan Ivanov', 'ivan@example.com', 'pro-eu'),
('Petar Petrov', 'petar@example.com', 'pro-russia');

SELECT * FROM AppUser;
GO

-- 4) User preferences
INSERT INTO UserPreference (UserId, Topic)
VALUES
(1, 'economy'),
(1, 'politics'),
(2, 'geopolitics');

SELECT * FROM UserPreference;
GO
