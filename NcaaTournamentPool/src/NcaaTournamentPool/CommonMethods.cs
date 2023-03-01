using System;
using System.Configuration;
using System.Web;
using System.Collections.Generic;
using System.IO;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;

namespace NcaaTournamentPool
{
    public static class CommonMethods
    {
        private const string SETPATH = "ncaatourneydb.mdb";
        private const string LOGPATH = "log.inc";

        private static AmazonDynamoDBConfig dynamoDBConfig;

        static CommonMethods()
        {
            dynamoDBConfig = new AmazonDynamoDBConfig();
            dynamoDBConfig.RegionEndpoint = Amazon.RegionEndpoint.USWest2;
        }

        public static async Task<Round[]> loadRoundsForLobby()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table roundTable = Table.LoadTable(dynamoDBClient, "ncaatournamentpool-rounds");

            CurrentStatus nowStatus = await loadCurrentStatus();

            var roundTableScanRequest = new ScanOperationConfig()
            {
                AttributesToGet = new List<string> { "round-number", "points-to-spend", "max-hold-over" },
            };
            var roundTableScan = roundTable.Scan(roundTableScanRequest);
            int recordCount = roundTableScan.Count;

            Round[] theRounds = new Round[recordCount];
            List<Document> roundDocuments = roundTableScan.Matches;

            for (int i = 0; i < recordCount; i++)
            {
                theRounds[i] = new Round();
                theRounds[i].roundNumber = roundDocuments[i]["round-number"].AsInt();
                theRounds[i].pointsToSpend = roundDocuments[i]["points-to-spend"].AsInt();
                theRounds[i].maxHoldOverPoints = roundDocuments[i]["max-hold-over"].AsInt();
                theRounds[i].roundOrder = new int[nowStatus.totalPlayers];

                if ((theRounds[i].roundNumber % 4) == 1)
                {
                    for (int j = 1; j <= nowStatus.totalPlayers; j++)
                    {
                        theRounds[i].roundOrder[j - 1] = j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 2)
                {
                    for (int j = 1; j <= nowStatus.totalPlayers; j++)
                    {
                        theRounds[i].roundOrder[j - 1] = nowStatus.totalPlayers + 1 - j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 3)
                {
                    for (int j = 1; j <= nowStatus.totalPlayers; j++)
                    {
                        if (j <= (nowStatus.totalPlayers / 2) + (nowStatus.totalPlayers % 2))
                            theRounds[i].roundOrder[j - 1] = nowStatus.totalPlayers / 2 + j;
                        else
                            theRounds[i].roundOrder[j - 1] = nowStatus.totalPlayers / 2 - (nowStatus.totalPlayers - j);
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 0)
                {
                    for (int j = 1; j <= nowStatus.totalPlayers; j++)
                    {
                        if (j <= (nowStatus.totalPlayers / 2))
                            theRounds[i].roundOrder[j - 1] = nowStatus.totalPlayers / 2 - j + 1;
                        else
                            theRounds[i].roundOrder[j - 1] = nowStatus.totalPlayers / 2 + (nowStatus.totalPlayers - j) + 1;
                    }
                }
            }
            return theRounds;

        }

        public static async Task<Player[]> loadPlayersForLobby()
        {
            CurrentStatus nowStatus = await loadCurrentStatus();

            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table userTable = Table.LoadTable(dynamoDBClient, "ncaatournamentpool-users");
            var userTableScanRequest = new ScanOperationConfig();
            var userTableScan = userTable.Scan(userTableScanRequest);
            List<Document> userDocuments = await userTableScan.GetRemainingAsync();

            var thePlayers = new List<Player>();
            foreach (Document currentUser in userDocuments)
            {
                var thisPlayer = new Player();
                thisPlayer.name = currentUser["name"].AsString();
                thisPlayer.pointsHeldOver = currentUser["points-held-over"].AsInt();
                thisPlayer.userId = currentUser["user-id"].AsInt();
                thisPlayer.color = currentUser["color"].AsString();
                thisPlayer.initialPickOrder = currentUser["initial-order"].AsInt();
                thePlayers.Add(thisPlayer); 
            }
            thePlayers.Sort((x, y) => x.initialPickOrder.CompareTo(y.initialPickOrder));

            return thePlayers.ToArray();
        }
        /*
        public static void resetDraft(HttpServerUtility theserver)
        {
            string DB = theserver.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbCommand olecommand = new OleDbCommand("resetDraftStatus", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            olecommand = new OleDbCommand("resetTeams", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            olecommand = new OleDbCommand("resetUserPoints", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            clearLog();


        }*/

        public static async Task<CurrentStatus> loadCurrentStatus()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table statusTable = Table.LoadTable(dynamoDBClient, "ncaatournamentpool-draftstatus");
            Table roundTable = Table.LoadTable(dynamoDBClient, "ncaatournamentpool-rounds");
            Table userTable = Table.LoadTable(dynamoDBClient, "ncaatournamentpool-users");

            var statusTableConfig = new GetItemOperationConfig
            {
                AttributesToGet = new List<string> { "current-round", "finished", "order-index" },
                ConsistentRead = true,
            };
            var statusTableRow = await statusTable.GetItemAsync(1, statusTableConfig);

            var roundTableConfig = new GetItemOperationConfig
            {
                AttributesToGet = new List<string> { "points-to-spend", "max-hold-over" },
                ConsistentRead = true,
            };
            var roundTableRow = await roundTable.GetItemAsync(1, roundTableConfig);

            var roundTableScanRequest = new ScanOperationConfig();
            /*{
                AttributesToGet = new List<string> { "round-number", "points-to-spend", "max-hold-over" },
            };*/
            var roundTableScan = roundTable.Scan(roundTableScanRequest);

            var userTableScanRequest = new ScanOperationConfig();
            var userTableScan = userTable.Scan(userTableScanRequest);

            CurrentStatus nowStatus = new CurrentStatus();

            nowStatus.round = statusTableRow["current-round"].AsInt();
            nowStatus.maxHoldOverPoints = roundTableRow["max-hold-over"].AsInt();
            nowStatus.pointsToSpend = roundTableRow["points-to-spend"].AsInt();
            nowStatus.totalPlayers = userTableScan.Count;
            nowStatus.currentOrderIndex = statusTableRow["order-index"].AsInt();
            nowStatus.finished = statusTableRow["finished"].AsBoolean();
            nowStatus.totalRounds = roundTableScan.Count;

            return nowStatus;
        }

        /*
        public static Team[] loadTeamsForBracketView(int bracketId)
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);
            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getBracketOfTeams", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            OleDbParameter parameters = new OleDbParameter("@BracketId", OleDbType.Integer);
            parameters.Value = bracketId;
            olecommand.Parameters.Add(parameters);

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            DataTable teamsTable = clubdataset.Tables["clubsdata"];

            int recordCount = teamsTable.Rows.Count;

            Team[] theTeams = new Team[recordCount];

            CurrentStatus nowStatus = new CurrentStatus();

            for (int i = 0; i < recordCount; i++)
            {
                theTeams[i] = new Team();
                theTeams[i].sCurveRank = Convert.ToInt32(teamsTable.Rows[i]["SCurveRank"]);
                theTeams[i].teamName = teamsTable.Rows[i]["TeamName"].ToString();
                theTeams[i].bracket = teamsTable.Rows[i]["Bracket"].ToString();
                theTeams[i].bracketId = Convert.ToInt32(teamsTable.Rows[i]["BracketId"].ToString());
                theTeams[i].eliminated = ((bool)teamsTable.Rows[i]["Eliminated"]);
                theTeams[i].rank = Convert.ToInt32(teamsTable.Rows[i]["TeamRank"].ToString());
                theTeams[i].wins = Convert.ToInt32(teamsTable.Rows[i]["TeamWins"].ToString());
                theTeams[i].losses = Convert.ToInt32(teamsTable.Rows[i]["TeamLosses"].ToString());
                theTeams[i].cost = Convert.ToInt32(teamsTable.Rows[i]["Cost"]);
                theTeams[i].pickedByPlayer = Convert.ToInt32(teamsTable.Rows[i]["SelectedBy"].ToString());
            }

            return theTeams;

        }

        public static void pickATeam(Team theTeam, Player thePlayer)
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbCommand olecommand = new OleDbCommand("selectTeam", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            OleDbParameter parameters = new OleDbParameter("@UserId", OleDbType.Integer);
            parameters.Value = thePlayer.userId;
            olecommand.Parameters.Add(parameters);

            parameters = new OleDbParameter("@SCurveRank", OleDbType.Integer);
            parameters.Value = theTeam.sCurveRank;
            olecommand.Parameters.Add(parameters);

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();
        }

        public static void advanceDraft(Team[] selectedTeams, Player thePlayer, CurrentStatus nowStatus)
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            string logstring = "<b>Round " + nowStatus.round.ToString() + " - " + thePlayer.firstName + " " + thePlayer.lastName + ":</b> ";

            bool first = true;

            foreach (Team thisTeam in selectedTeams)
            {
                pickATeam(thisTeam, thePlayer);
                if (!first) logstring = logstring + ", ";
                logstring = logstring + thisTeam.teamName + " (" + thisTeam.rank.ToString() + ", " + thisTeam.bracket + ", " + (thisTeam.cost).ToString() + " points)";
                first = false;
            }

            logstring = logstring + " <i>Left " + thePlayer.pointsHeldOver.ToString() + " points</i>";
            if (nowStatus.currentOrderIndex >= nowStatus.totalPlayers - 1)
            {
                nowStatus.currentOrderIndex = 0;
                nowStatus.round++;
                logstring = logstring + "<br />";
            }
            else
            {
                nowStatus.currentOrderIndex++;
            }

            OleDbCommand olecommand;
            OleDbParameter parameters;

            if (nowStatus.round <= nowStatus.totalRounds)
            {
                olecommand = new OleDbCommand("updateStatus", dbConnect);
                olecommand.CommandType = CommandType.StoredProcedure;

                parameters = new OleDbParameter("@Round", OleDbType.Integer);
                parameters.Value = nowStatus.round;
                olecommand.Parameters.Add(parameters);

                parameters = new OleDbParameter("@OrderIndex", OleDbType.Integer);
                parameters.Value = nowStatus.currentOrderIndex;
                olecommand.Parameters.Add(parameters);

            }
            else
            {
                olecommand = new OleDbCommand("finishDraft", dbConnect);
                olecommand.CommandType = CommandType.StoredProcedure;
            }

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            olecommand = new OleDbCommand("updateUserStatus", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            parameters = new OleDbParameter("@PointsHeldOver", OleDbType.Integer);
            parameters.Value = thePlayer.pointsHeldOver;
            olecommand.Parameters.Add(parameters);

            parameters = new OleDbParameter("@UserId", OleDbType.Integer);
            parameters.Value = thePlayer.userId;
            olecommand.Parameters.Add(parameters);

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();
            writeToLog(logstring);

            if (nowStatus.round > nowStatus.totalRounds)
            {
                runEndOfDraftLottery();
            }

        }

        public static void runEndOfDraftLottery()
        {

            string logstring = "<b>End of Draft Lottery</b><br />";

            Player[] thePlayers = loadPlayersForLobby();
            Team[] theTeams = loadTeamsForLotteryAsTeams();

            int totalEntries = 0;
            int entriesToDeduct = 1;

            foreach (Player thisPlayer in thePlayers)
            {
                // Adjust for the 2011 doubling of points
                thisPlayer.pointsHeldOver = Convert.ToInt32(Math.Floor(thisPlayer.pointsHeldOver / 2.0));

                totalEntries = totalEntries + thisPlayer.pointsHeldOver;
            }

            if (totalEntries == 0)
            {
                foreach (Player thisPlayer in thePlayers)
                {
                    thisPlayer.pointsHeldOver = 1;
                    totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                }
            }

            foreach (Player thisPlayer in thePlayers)
            {
                logstring = logstring + "<b>" + thisPlayer.firstName + " " + thisPlayer.lastName + "</b> has <b>" + thisPlayer.pointsHeldOver.ToString() + "</b> entries.<br />";
            }

            logstring = logstring + "<br />";

            while (totalEntries < theTeams.Length)
            {
                totalEntries = 0;
                foreach (Player thisPlayer in thePlayers)
                {
                    thisPlayer.pointsHeldOver = thisPlayer.pointsHeldOver * 2;
                    totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                }
                entriesToDeduct = entriesToDeduct * 2;
            }

            int[] theLotteryArray;

            Random meRandom = new Random();

            int teamsDone = 0;



            foreach (Team thisTeam in theTeams)
            {

                while (totalEntries < (theTeams.Length - teamsDone))
                {
                    if (totalEntries == 0)
                    {
                        foreach (Player thisPlayer in thePlayers)
                        {
                            thisPlayer.pointsHeldOver = 1;
                            totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                            entriesToDeduct = 1;
                        }
                    }
                    else
                    {
                        totalEntries = 0;
                        foreach (Player thisPlayer in thePlayers)
                        {
                            thisPlayer.pointsHeldOver = thisPlayer.pointsHeldOver * 2;
                            totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                        }
                        entriesToDeduct = entriesToDeduct * 2;
                    }
                }

                theLotteryArray = new int[totalEntries];

                int startIndex = 0;
                int playerIndex = 0;

                foreach (Player thisPlayer in thePlayers)
                {
                    if (thisPlayer.pointsHeldOver != 0)
                    {
                        for (int i = startIndex; i < startIndex + thisPlayer.pointsHeldOver; i++)
                        {
                            theLotteryArray[i] = playerIndex;
                        }
                    }
                    startIndex = startIndex + thisPlayer.pointsHeldOver;
                    playerIndex++;
                }


                int givenTo = theLotteryArray[meRandom.Next() % totalEntries];

                pickATeam(thisTeam, thePlayers[givenTo]);

                logstring = logstring + thisTeam.teamName + " (" + thisTeam.rank.ToString() + ", " + thisTeam.bracket + ", " + (thisTeam.cost).ToString() + " points) ";
                logstring = logstring + "is awarded to <b>" + thePlayers[givenTo].firstName + " " + thePlayers[givenTo].lastName + "</b>.<br />";

                thePlayers[givenTo].pointsHeldOver = thePlayers[givenTo].pointsHeldOver - entriesToDeduct;

                totalEntries = 0;

                foreach (Player thisPlayer in thePlayers)
                {
                    totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                }

                teamsDone++;
            }

            OleDbCommand olecommand;

            olecommand = new OleDbCommand("finishDraft", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            writeToLog(logstring);
        }

        public static Team[] loadTeamsForLotteryAsTeams()
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getUnselectedTeamsForLottery", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            DataTable teamsTable = clubdataset.Tables["clubsdata"];

            int recordCount = teamsTable.Rows.Count;

            Team[] theTeams = new Team[recordCount];

            CurrentStatus nowStatus = new CurrentStatus();

            for (int i = 0; i < recordCount; i++)
            {
                theTeams[i] = new Team();
                theTeams[i].sCurveRank = Convert.ToInt32(teamsTable.Rows[i]["SCurveRank"]);
                theTeams[i].teamName = teamsTable.Rows[i]["TeamName"].ToString();
                theTeams[i].bracket = teamsTable.Rows[i]["Bracket"].ToString();
                theTeams[i].bracketId = Convert.ToInt32(teamsTable.Rows[i]["BracketId"].ToString());
                theTeams[i].rank = Convert.ToInt32(teamsTable.Rows[i]["TeamRank"].ToString());
                theTeams[i].wins = Convert.ToInt32(teamsTable.Rows[i]["TeamWins"].ToString());
                theTeams[i].losses = Convert.ToInt32(teamsTable.Rows[i]["TeamLosses"].ToString());
                theTeams[i].cost = Convert.ToInt32(teamsTable.Rows[i]["Cost"]);
                theTeams[i].pickedByPlayer = 0;
            }

            return theTeams;
        }

        private static void writeToLog(string logstring)
        {
            string logfile = HttpContext.Current.Server.MapPath(LOGPATH);
            StreamWriter filetowrite = new StreamWriter(logfile, true);
            filetowrite.WriteLine(logstring + "<br />");
            filetowrite.Close();
        }

        private static void clearLog()
        {
            string logfile = HttpContext.Current.Server.MapPath(LOGPATH);
            StreamWriter filetowrite = new StreamWriter(logfile, false);
            filetowrite.Close();
        }

        public static string loadBracketName(int bracketId)
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbCommand olecommand = new OleDbCommand("getBracketName", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            OleDbParameter parameters = new OleDbParameter("@BracketId", OleDbType.Integer);
            parameters.Value = bracketId;
            olecommand.Parameters.Add(parameters);

            string name = null;

            try
            {
                dbConnect.Open();

                name = olecommand.ExecuteScalar().ToString();
            }
            finally
            {
                if (dbConnect.State != ConnectionState.Closed)
                {
                    dbConnect.Close();
                }
            }

            return name;
        }

        public static DataTable loadUsers(HttpServerUtility theserver)
        {
            string DB = theserver.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getAllUsers", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            return clubdataset.Tables["clubsdata"];
        }

        public static DataTable loadRounds(HttpServerUtility theserver)
        {
            string DB = theserver.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getAllRounds", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            return clubdataset.Tables["clubsdata"];
        }

        public static DataTable loadTeamsDb()
        {
            string DB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getTeamsForEditing", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            return clubdataset.Tables["clubsdata"];

        }

        public static DataTable loadUnselectedTeams(HttpServerUtility theserver)
        {
            string DB = theserver.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + DB);

            OleDbDataAdapter dbadapter;
            DataSet clubdataset;

            OleDbCommand olecommand = new OleDbCommand("getUnselectedTeams", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            dbadapter = new OleDbDataAdapter(olecommand);

            clubdataset = new DataSet();
            dbadapter.Fill(clubdataset, "clubsdata");

            return clubdataset.Tables["clubsdata"];

        }

        public static void updateTeamInfo(Team updatedTeam)
        {
            string OFFICERDB = HttpContext.Current.Server.MapPath(SETPATH);
            dbConnect = new OleDbConnection(CONNECTSTRING + OFFICERDB);

            OleDbCommand olecommand = new OleDbCommand("updateTeamInfo", dbConnect);
            olecommand.CommandType = CommandType.StoredProcedure;

            OleDbParameter parameters = new OleDbParameter("@TeamName", OleDbType.Char);
            parameters.Value = updatedTeam.teamName;
            olecommand.Parameters.Add(parameters);

            parameters = new OleDbParameter("@TeamWins", OleDbType.Integer);
            parameters.Value = updatedTeam.wins;
            olecommand.Parameters.Add(parameters);

            parameters = new OleDbParameter("@TeamLosses", OleDbType.Integer);
            parameters.Value = updatedTeam.losses;
            olecommand.Parameters.Add(parameters);

            parameters = new OleDbParameter("@Eliminated", OleDbType.Boolean);
            parameters.Value = updatedTeam.eliminated;
            olecommand.Parameters.Add(parameters);


            parameters = new OleDbParameter("@SCurveRank", OleDbType.Integer);
            parameters.Value = updatedTeam.sCurveRank;
            olecommand.Parameters.Add(parameters);

            olecommand.Connection.Open();

            olecommand.ExecuteNonQuery();

            dbConnect.Close();

            return;
        }

        public static Dictionary<int, List<Team>> SortTeamsByRank(Team[] retrievedTeams)
        {
            Dictionary<int, List<Team>> result = new Dictionary<int, List<Team>>();

            foreach (Team team in retrievedTeams)
            {
                if (!result.ContainsKey(team.rank))
                {
                    result[team.rank] = new List<Team>();
                }

                result[team.rank].Add(team);
            }

            return result;
        }
        */
    }
}
