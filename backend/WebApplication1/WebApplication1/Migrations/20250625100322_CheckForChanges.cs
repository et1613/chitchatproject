using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    /// <inheritdoc />
    public partial class CheckForChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Messages",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 952, DateTimeKind.Utc).AddTicks(7104),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 987, DateTimeKind.Utc).AddTicks(4376));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatRooms",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 948, DateTimeKind.Utc).AddTicks(6169),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 983, DateTimeKind.Utc).AddTicks(5129));

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "Attachments",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "json")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "FileHash",
                table: "Attachments",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 939, DateTimeKind.Utc).AddTicks(2888),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 976, DateTimeKind.Utc).AddTicks(1141));

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers",
                column: "BlockedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers",
                column: "BlockerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminId",
                table: "ChatRooms",
                column: "AdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverId",
                table: "FriendRequests",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderId",
                table: "FriendRequests",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminId",
                table: "ChatRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderId",
                table: "FriendRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "FileHash",
                table: "Attachments");

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

            migrationBuilder.AlterColumn<DateTime>(
                name: "Timestamp",
                table: "Messages",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 987, DateTimeKind.Utc).AddTicks(4376),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 952, DateTimeKind.Utc).AddTicks(7104));

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "ChatRooms",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 983, DateTimeKind.Utc).AddTicks(5129),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 948, DateTimeKind.Utc).AddTicks(6169));

            migrationBuilder.AlterColumn<string>(
                name: "Metadata",
                table: "Attachments",
                type: "json",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(2025, 6, 24, 13, 10, 51, 976, DateTimeKind.Utc).AddTicks(1141),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValue: new DateTime(2025, 6, 25, 10, 3, 21, 939, DateTimeKind.Utc).AddTicks(2888));

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockedUserId",
                table: "BlockedUsers",
                column: "BlockedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_AspNetUsers_BlockerUserId",
                table: "BlockedUsers",
                column: "BlockerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_AspNetUsers_AdminId",
                table: "ChatRooms",
                column: "AdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_ReceiverId",
                table: "FriendRequests",
                column: "ReceiverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_AspNetUsers_SenderId",
                table: "FriendRequests",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AspNetUsers_SenderId",
                table: "Messages",
                column: "SenderId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages",
                column: "ReplyToMessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
