CREATE TABLE [dbo].[Clone]
(
	[Id] INT IDENTITY(1,1) PRIMARY KEY, 
    [Nodes] INT NULL , 
    [Source] TEXT NULL, 
    [ACT] TEXT NULL, 
    [CloneClusterId] INT NULL
)
