using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGroupChatToChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_NotificationSettings_NotificationSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_UserPreferences_UserPreferencesUserId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_UserSettings_UserSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "BlockedIpAddresses");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NotificationSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserPreferencesUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_UserSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotificationSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserPreferencesUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UserSettingsUserId",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "EnabledTypes",
                table: "NotificationPreferences",
                newName: "EnabledTypesJson");

            migrationBuilder.RenameColumn(
                name: "EnabledChannels",
                table: "NotificationPreferences",
                newName: "EnabledChannelsJson");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZone",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "UTC")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "light")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldDefaultValue: "en")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "UserSettings",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsReadReceiptsPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsOnlineStatusPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsEmailPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId1",
                table: "UserPreferences",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSuccessful",
                table: "UserActivities",
                type: "tinyint(1)",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "UserActivities",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BlockedSenders",
                table: "NotificationPreferences",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Messages",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 987, DateTimeKind.Utc).AddTicks(4376),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 575, DateTimeKind.Utc).AddTicks(7926));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatRooms",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 983, DateTimeKind.Utc).AddTicks(5129),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 571, DateTimeKind.Utc).AddTicks(7534));

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupChat",
                table: "ChatRooms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 976, DateTimeKind.Utc).AddTicks(1141),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 564, DateTimeKind.Utc).AddTicks(1810));

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                table: "AspNetUsers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationTokenExpiresAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_UserId1",
                table: "UserSettings",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferences_UserId1",
                table: "UserPreferences",
                column: "UserId1");

            migrationBuilder.AddForeignKey(
                name: "FK_UserPreferences_AspNetUsers_UserId1",
                table: "UserPreferences",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSettings_AspNetUsers_UserId1",
                table: "UserSettings",
                column: "UserId1",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserPreferences_AspNetUsers_UserId1",
                table: "UserPreferences");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSettings_AspNetUsers_UserId1",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_UserId1",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserPreferences_UserId1",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "UserId1",
                table: "UserPreferences");

            migrationBuilder.DropColumn(
                name: "IsGroupChat",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenExpiresAt",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "EnabledTypesJson",
                table: "NotificationPreferences",
                newName: "EnabledTypes");

            migrationBuilder.RenameColumn(
                name: "EnabledChannelsJson",
                table: "NotificationPreferences",
                newName: "EnabledChannels");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZone",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                defaultValue: "UTC",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                defaultValue: "light",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                table: "UserSettings",
                type: "longtext",
                nullable: false,
                defaultValue: "en",
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<bool>(
                name: "IsReadReceiptsPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsOnlineStatusPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsEmailPublic",
                table: "UserPreferences",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSuccessful",
                table: "UserActivities",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.UpdateData(
                table: "UserActivities",
                keyColumn: "Description",
                keyValue: null,
                column: "Description",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "UserActivities",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BlockedSenders",
                table: "NotificationPreferences",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Messages",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 575, DateTimeKind.Utc).AddTicks(7926),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 987, DateTimeKind.Utc).AddTicks(4376));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatRooms",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 571, DateTimeKind.Utc).AddTicks(7534),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 983, DateTimeKind.Utc).AddTicks(5129));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 11, 8, 19, 46, 564, DateTimeKind.Utc).AddTicks(1810),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 976, DateTimeKind.Utc).AddTicks(1141));

            migrationBuilder.AddColumn<string>(
                name: "NotificationSettingsUserId",
                table: "AspNetUsers",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserPreferencesUserId",
                table: "AspNetUsers",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "UserSettingsUserId",
                table: "AspNetUsers",
                type: "varchar(255)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BlockedIpAddresses",
                columns: table => new
                {
                    IpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BlockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedIpAddresses", x => x.IpAddress);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NotificationSettingsUserId",
                table: "AspNetUsers",
                column: "NotificationSettingsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserPreferencesUserId",
                table: "AspNetUsers",
                column: "UserPreferencesUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_UserSettingsUserId",
                table: "AspNetUsers",
                column: "UserSettingsUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BlockedIpAddresses_ExpiresAt",
                table: "BlockedIpAddresses",
                column: "ExpiresAt");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_NotificationSettings_NotificationSettingsUserId",
                table: "AspNetUsers",
                column: "NotificationSettingsUserId",
                principalTable: "NotificationSettings",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UserPreferences_UserPreferencesUserId",
                table: "AspNetUsers",
                column: "UserPreferencesUserId",
                principalTable: "UserPreferences",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_UserSettings_UserSettingsUserId",
                table: "AspNetUsers",
                column: "UserSettingsUserId",
                principalTable: "UserSettings",
                principalColumn: "UserId");
        }
    }
}
