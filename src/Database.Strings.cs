using System.Data.Common;

namespace WTelegram;

#pragma warning disable CS1591, CA1069
public enum SqlCommands { Detect = -1, Sqlite = 0, Postgresql = 0, SQLServer = 1, MySQL = 2 };
#pragma warning restore CS1591, CA1069
internal partial class Database
{
	const int DbSetup = 0, LoadSession = 1, SaveSession = 2, LoadUpdates = 3, DelUpdates = 4, SaveUpdates = 5;
	const int LoadMBox = 6, SaveMBox = 7, LoadUser = 8, SaveUser = 9, LoadChat = 10, SaveChat = 11;
	public static readonly string[][] DefaultSqlCommands =
	[
		[ // Sqlite or PostgreSQL
/*DbSetup*/	"CREATE TABLE IF NOT EXISTS WTB_MBoxState (MBox BIGINT NOT NULL PRIMARY KEY, pts INT NOT NULL, access_hash BIGINT NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Session (Name VARCHAR(32) NOT NULL PRIMARY KEY, Data BYTEA NOT NULL, LastUpdateId INT NOT NULL, AllowedUpdates INT NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Updates (Id INT NOT NULL PRIMARY KEY, TLData BYTEA NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Users (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, LanguageCode VARCHAR(16) NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Chats (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, Type INT NOT NULL) ;\n",
/*LoadSess*/"SELECT Data, LastUpdateId, AllowedUpdates FROM WTB_Session WHERE Name = 'WTelegramBot'",
/*SaveSess*/"INSERT INTO WTB_Session (Name, Data, LastUpdateId, AllowedUpdates) VALUES ('WTelegramBot', @Data, @LastUpdateId, @AllowedUpdates) ON CONFLICT (Name) DO UPDATE SET Data = EXCLUDED.Data, LastUpdateId = EXCLUDED.LastUpdateId, AllowedUpdates = EXCLUDED.AllowedUpdates",
/*LoadUpd*/	"SELECT Id, TLData FROM WTB_Updates ORDER BY Id",
/*DelUpd*/	"DELETE FROM WTB_Updates",
/*SaveUpd*/	"INSERT INTO WTB_Updates (Id, TLData) VALUES (@Id, @TLData)",
/*LoadMBox*/"SELECT MBox, pts, access_hash FROM WTB_MBoxState",
/*SaveMBox*/"INSERT INTO WTB_MBoxState(mbox, pts, access_hash) VALUES(@MBox, @pts, @access_hash) ON CONFLICT(MBox) DO UPDATE SET pts=EXCLUDED.pts, access_hash=EXCLUDED.access_hash",
/*LoadUser*/"SELECT AccessHash, Flags, FirstName, LastName, Username, LanguageCode FROM WTB_Users WHERE Id = @Id;",
/*SaveUser*/"INSERT INTO WTB_Users (Id, AccessHash, Flags, FirstName, LastName, Username, LanguageCode) VALUES(@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @LanguageCode) ON CONFLICT(Id) DO UPDATE SET AccessHash=EXCLUDED.AccessHash, Flags=EXCLUDED.Flags, FirstName=EXCLUDED.FirstName, LastName=EXCLUDED.LastName, Username=EXCLUDED.Username, LanguageCode=EXCLUDED.LanguageCode",
/*LoadChat*/"SELECT AccessHash, Flags, FirstName, LastName, Username, Type FROM WTB_Chats WHERE Id = -@Id OR Id = -1000000000000-@Id;",
/*SaveChat*/"INSERT INTO WTB_Chats (Id, AccessHash, Flags, FirstName, LastName, Username, Type) VALUES(@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @Type) ON CONFLICT(Id) DO UPDATE SET AccessHash=EXCLUDED.AccessHash, Flags=EXCLUDED.Flags, FirstName=EXCLUDED.FirstName, LastName=EXCLUDED.LastName, Username=EXCLUDED.Username, Type=EXCLUDED.Type",
		],
		[ // SQL Server
/*DbSetup*/	"IF OBJECT_ID('WTB_MBoxState') IS NULL CREATE TABLE WTB_MBoxState (MBox BIGINT NOT NULL PRIMARY KEY, pts INT NOT NULL, access_hash BIGINT NOT NULL) ;\n" +
			"IF OBJECT_ID('WTB_Session')   IS NULL CREATE TABLE WTB_Session (Name VARCHAR(32) NOT NULL PRIMARY KEY, Data VARBINARY(MAX) NOT NULL, LastUpdateId INT NOT NULL, AllowedUpdates INT NOT NULL) ;\n" +
			"IF OBJECT_ID('WTB_Updates')   IS NULL CREATE TABLE WTB_Updates (Id INT NOT NULL PRIMARY KEY, TLData VARBINARY(MAX) NOT NULL) ;\n" +
			"IF OBJECT_ID('WTB_Users')     IS NULL CREATE TABLE WTB_Users (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, LanguageCode VARCHAR(16) NOT NULL) ;\n" +
			"IF OBJECT_ID('WTB_Chats')     IS NULL CREATE TABLE WTB_Chats (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, Type INT NOT NULL) ;\n",
/*LoadSess*/"SELECT Data, LastUpdateId, AllowedUpdates FROM WTB_Session WHERE Name = 'WTelegramBot'",
/*SaveSess*/"MERGE INTO WTB_Session USING (VALUES ('WTelegramBot', @Data, @LastUpdateId, @AllowedUpdates)) AS NEW (Name, Data, LastUpdateId, AllowedUpdates) ON WTB_Session.Name = NEW.Name\nWHEN MATCHED THEN UPDATE SET Data = NEW.Data, LastUpdateId = NEW.LastUpdateId, AllowedUpdates = NEW.AllowedUpdates\nWHEN NOT MATCHED THEN INSERT (Name, Data, LastUpdateId, AllowedUpdates) VALUES (NEW.Name, NEW.Data, NEW.LastUpdateId, NEW.AllowedUpdates);",
/*LoadUpd*/	"SELECT Id, TLData FROM WTB_Updates ORDER BY Id",
/*DelUpd*/	"DELETE FROM WTB_Updates",
/*SaveUpd*/	"INSERT INTO WTB_Updates (Id, TLData) VALUES (@Id, @TLData)",
/*LoadMBox*/"SELECT MBox, pts, access_hash FROM WTB_MBoxState",
/*SaveMBox*/"MERGE INTO WTB_MBoxState USING (VALUES (@MBox, @pts, @access_hash)) AS NEW (mbox, pts, access_hash) ON WTB_MBoxState.MBox = NEW.MBox\nWHEN MATCHED THEN UPDATE SET pts=NEW.pts, access_hash=NEW.access_hash\nWHEN NOT MATCHED THEN INSERT (mbox, pts, access_hash) VALUES (NEW.mbox, NEW.pts, NEW.access_hash);",
/*LoadUser*/"SELECT AccessHash, Flags, FirstName, LastName, Username, LanguageCode FROM WTB_Users WHERE Id = @Id;",
/*SaveUser*/"MERGE INTO WTB_Users USING (VALUES (@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @LanguageCode)) AS NEW (Id, AccessHash, Flags, FirstName, LastName, Username, LanguageCode) ON WTB_Users.Id = NEW.Id\nWHEN MATCHED THEN UPDATE SET AccessHash=NEW.AccessHash, Flags=NEW.Flags, FirstName=NEW.FirstName, LastName=NEW.LastName, Username=NEW.Username, LanguageCode=NEW.LanguageCode\nWHEN NOT MATCHED THEN INSERT (Id, AccessHash, Flags, FirstName, LastName, Username, LanguageCode) VALUES (NEW.Id, NEW.AccessHash, NEW.Flags, NEW.FirstName, NEW.LastName, NEW.Username, NEW.LanguageCode);",
/*LoadChat*/"SELECT AccessHash, Flags, FirstName, LastName, Username, Type FROM WTB_Chats WHERE Id = -@Id OR Id = -1000000000000-@Id;",
/*SaveChat*/"MERGE INTO WTB_Chats USING (VALUES (@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @Type)) AS NEW (Id, AccessHash, Flags, FirstName, LastName, Username, Type) ON WTB_Chats.Id = NEW.Id\nWHEN MATCHED THEN UPDATE SET AccessHash=NEW.AccessHash, Flags=NEW.Flags, FirstName=NEW.FirstName, LastName=NEW.LastName, Username=NEW.Username, Type=NEW.Type\nWHEN NOT MATCHED THEN INSERT (Id, AccessHash, Flags, FirstName, LastName, Username, Type) VALUES (NEW.Id, NEW.AccessHash, NEW.Flags, NEW.FirstName, NEW.LastName, NEW.Username, NEW.Type);",
		],
		[ // MySQL
/*DbSetup*/	"CREATE TABLE IF NOT EXISTS WTB_MBoxState (MBox BIGINT NOT NULL PRIMARY KEY, pts INT NOT NULL, access_hash BIGINT NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Session (Name VARCHAR(32) NOT NULL PRIMARY KEY, Data BLOB NOT NULL, LastUpdateId INT NOT NULL, AllowedUpdates INT NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Updates (Id INT NOT NULL PRIMARY KEY, TLData BLOB NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Users (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, LanguageCode VARCHAR(16) NOT NULL) ;\n" +
			"CREATE TABLE IF NOT EXISTS WTB_Chats (Id BIGINT NOT NULL PRIMARY KEY, AccessHash BIGINT NOT NULL, Flags INT NOT NULL, FirstName VARCHAR(255) NOT NULL, LastName VARCHAR(255) NOT NULL, Username VARCHAR(255) NOT NULL, Type INT NOT NULL) ;\n",
/*LoadSess*/"SELECT Data, LastUpdateId, AllowedUpdates FROM WTB_Session WHERE Name = 'WTelegramBot'",
/*SaveSess*/"REPLACE INTO WTB_Session (Name, Data, LastUpdateId, AllowedUpdates) VALUES ('WTelegramBot', @Data, @LastUpdateId, @AllowedUpdates)",
/*LoadUpd*/	"SELECT Id, TLData FROM WTB_Updates ORDER BY Id",
/*DelUpd*/	"DELETE FROM WTB_Updates",
/*SaveUpd*/	"INSERT INTO WTB_Updates (Id, TLData) VALUES (@Id, @TLData)",
/*LoadMBox*/"SELECT MBox, pts, access_hash FROM WTB_MBoxState",
/*SaveMBox*/"REPLACE INTO WTB_MBoxState(mbox, pts, access_hash) VALUES(@MBox, @pts, @access_hash)",
/*LoadUser*/"SELECT AccessHash, Flags, FirstName, LastName, Username, LanguageCode FROM WTB_Users WHERE Id = @Id;",
/*SaveUser*/"REPLACE INTO WTB_Users (Id, AccessHash, Flags, FirstName, LastName, Username, LanguageCode) VALUES(@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @LanguageCode)",
/*LoadChat*/"SELECT AccessHash, Flags, FirstName, LastName, Username, Type FROM WTB_Chats WHERE Id = -@Id OR Id = -1000000000000-@Id;",
/*SaveChat*/"REPLACE INTO WTB_Chats (Id, AccessHash, Flags, FirstName, LastName, Username, Type) VALUES(@Id, @AccessHash, @Flags, @FirstName, @LastName, @Username, @Type)",
		]
	];

	static readonly Dictionary<string, SqlCommands> DetectMapping = new()
	{
		["sqlite"] = SqlCommands.Sqlite,
		["postgre"] = SqlCommands.Postgresql,
		["pgsql"] = SqlCommands.Postgresql,
		[".sqlclient"] = SqlCommands.SQLServer,
		["sqlserver"] = SqlCommands.SQLServer,
		["mysql"] = SqlCommands.MySQL,
	};

	internal static SqlCommands DetectType(DbConnection dbConnection)
	{
		var type = dbConnection.GetType().FullName!;
		foreach (var mapping in DetectMapping)
			if (type.IndexOf(mapping.Key, StringComparison.OrdinalIgnoreCase) >= 0)
				return mapping.Value;
		return 0;
	}
}
