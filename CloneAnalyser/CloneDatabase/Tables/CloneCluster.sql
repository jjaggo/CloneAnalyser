CREATE TABLE [dbo].[CloneCluster]
(
	[Id] INT IDENTITY(1,1) PRIMARY KEY, 
    [TemplateCloneId] INT NULL, 
    [CloneCount] INT NOT NULL DEFAULT 0
)
