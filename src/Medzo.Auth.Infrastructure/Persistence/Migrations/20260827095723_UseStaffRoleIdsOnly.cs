using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medzo.Auth.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseStaffRoleIdsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SELECT CASE r.[Name]
                    WHEN N'Admin' THEN N'001'
                    WHEN N'Pharmacist' THEN N'002'
                    WHEN N'InventoryManager' THEN N'003'
                END AS [RolesId], ur.[UsersId]
                INTO #StaffUserRoles
                FROM [dbo].[UserRoles] ur
                INNER JOIN [dbo].[Roles] r ON r.[Id] = ur.[RolesId]
                WHERE r.[Name] IN (N'Admin', N'Pharmacist', N'InventoryManager');

                DROP TABLE [dbo].[UserRoles];
                DROP TABLE [dbo].[Roles];

                CREATE TABLE [dbo].[Roles]
                (
                    [Id] nchar(3) NOT NULL,
                    [Name] nvarchar(50) NOT NULL,
                    [Description] nvarchar(256) NULL,
                    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_Roles_Name] ON [dbo].[Roles] ([Name]);

                INSERT INTO [dbo].[Roles] ([Id], [Name], [Description]) VALUES
                    (N'001', N'Admin', N'System administrator'),
                    (N'002', N'Pharmacist', N'Licensed pharmacist'),
                    (N'003', N'InventoryManager', N'Inventory manager');

                CREATE TABLE [dbo].[UserRoles]
                (
                    [RolesId] nchar(3) NOT NULL,
                    [UsersId] uniqueidentifier NOT NULL,
                    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([RolesId], [UsersId]),
                    CONSTRAINT [FK_UserRoles_Roles_RolesId]
                        FOREIGN KEY ([RolesId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_UserRoles_Users_UsersId]
                        FOREIGN KEY ([UsersId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_UserRoles_UsersId] ON [dbo].[UserRoles] ([UsersId]);

                INSERT INTO [dbo].[UserRoles] ([RolesId], [UsersId])
                SELECT [RolesId], [UsersId] FROM #StaffUserRoles;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SELECT CASE r.[Name]
                    WHEN N'Admin' THEN CAST('11111111-1111-1111-1111-111111111111' AS uniqueidentifier)
                    WHEN N'Pharmacist' THEN CAST('22222222-2222-2222-2222-222222222222' AS uniqueidentifier)
                    WHEN N'InventoryManager' THEN CAST('33333333-3333-3333-3333-333333333333' AS uniqueidentifier)
                END AS [RolesId], ur.[UsersId]
                INTO #PreviousUserRoles
                FROM [dbo].[UserRoles] ur
                INNER JOIN [dbo].[Roles] r ON r.[Id] = ur.[RolesId];

                DROP TABLE [dbo].[UserRoles];
                DROP TABLE [dbo].[Roles];

                CREATE TABLE [dbo].[Roles]
                (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(50) NOT NULL,
                    [Description] nvarchar(256) NULL,
                    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_Roles_Name] ON [dbo].[Roles] ([Name]);

                INSERT INTO [dbo].[Roles] ([Id], [Name], [Description]) VALUES
                    ('11111111-1111-1111-1111-111111111111', N'Admin', N'System administrator'),
                    ('22222222-2222-2222-2222-222222222222', N'Pharmacist', N'Licensed pharmacist'),
                    ('33333333-3333-3333-3333-333333333333', N'InventoryManager', N'Inventory manager');

                CREATE TABLE [dbo].[UserRoles]
                (
                    [RolesId] uniqueidentifier NOT NULL,
                    [UsersId] uniqueidentifier NOT NULL,
                    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([RolesId], [UsersId]),
                    CONSTRAINT [FK_UserRoles_Roles_RolesId]
                        FOREIGN KEY ([RolesId]) REFERENCES [dbo].[Roles] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_UserRoles_Users_UsersId]
                        FOREIGN KEY ([UsersId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_UserRoles_UsersId] ON [dbo].[UserRoles] ([UsersId]);

                INSERT INTO [dbo].[UserRoles] ([RolesId], [UsersId])
                SELECT [RolesId], [UsersId] FROM #PreviousUserRoles;
                """);
        }
    }
}
