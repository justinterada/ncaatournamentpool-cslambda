using System;
using System.Configuration;
using System.Web;
using System.Collections.Generic;
using System.IO;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using System.Data;
using Amazon.DynamoDBv2.Model;
using NcaaTournamentPool.Shared;
using System.Text;

namespace NcaaTournamentPool
{
    public static class CommonMethods
    {
        private static AmazonDynamoDBConfig dynamoDBConfig;

        static CommonMethods()
        {
            dynamoDBConfig = new AmazonDynamoDBConfig();
            dynamoDBConfig.RegionEndpoint = Amazon.RegionEndpoint.USWest2;
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

        private static string GetTableName(string table)
        {
            return string.Format("ncaatournamentpool-{0}", table);
        }

        public static async Task<CurrentStatus> loadCurrentStatus()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table statusTable = Table.LoadTable(dynamoDBClient, GetTableName("draftstatus"));
            Table roundTable = Table.LoadTable(dynamoDBClient, GetTableName("rounds"));
            Table userTable = Table.LoadTable(dynamoDBClient, GetTableName("users"));

            var statusTableConfig = new GetItemOperationConfig
            {
                AttributesToGet = new List<string> { "current-round", "finished", "order-index", "log" },
                ConsistentRead = true,
            };
            var statusTableRow = await statusTable.GetItemAsync(1, statusTableConfig);
            CurrentStatus nowStatus = new CurrentStatus();
            nowStatus.round = statusTableRow["current-round"].AsInt();
            nowStatus.currentOrderIndex = statusTableRow["order-index"].AsInt();
            nowStatus.finished = statusTableRow["finished"].AsBoolean();
            nowStatus.log = statusTableRow["log"].AsString();

            var userTableScanRequest = new ScanOperationConfig();
            var userTableScan = userTable.Scan(userTableScanRequest);
            List<Document> userDocuments = await userTableScan.GetRemainingAsync();
            userDocuments.Sort((x, y) => x["initial-order"].AsInt().CompareTo(y["initial-order"].AsInt()));

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
            nowStatus.players = thePlayers.ToArray();

            var roundTableScanRequest = new ScanOperationConfig();
            var roundTableScan = roundTable.Scan(roundTableScanRequest);
            int recordCount = roundTableScan.Count;

            Round[] theRounds = new Round[recordCount];
            List<Document> roundDocuments = await roundTableScan.GetRemainingAsync();
            roundDocuments.Sort((x, y) => x["round-number"].AsInt().CompareTo(y["round-number"].AsInt()));

            for (int i = 0; i < recordCount; i++)
            {
                theRounds[i] = new Round();
                theRounds[i].roundNumber = roundDocuments[i]["round-number"].AsInt();
                theRounds[i].pointsToSpend = roundDocuments[i]["points-to-spend"].AsInt();
                theRounds[i].maxHoldOverPoints = roundDocuments[i]["max-hold-over"].AsInt();

                if (nowStatus.round == theRounds[i].roundNumber)
                {
                    nowStatus.currentRound = theRounds[i];
                }

                theRounds[i].roundOrder = new int[thePlayers.Count()];

                if ((theRounds[i].roundNumber % 4) == 1)
                {
                    for (int j = 1; j <= thePlayers.Count(); j++)
                    {
                        theRounds[i].roundOrder[j - 1] = j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 2)
                {
                    for (int j = 1; j <= thePlayers.Count(); j++)
                    {
                        theRounds[i].roundOrder[j - 1] = thePlayers.Count() + 1 - j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 3)
                {
                    for (int j = 1; j <= thePlayers.Count(); j++)
                    {
                        if (j <= (thePlayers.Count() / 2) + (thePlayers.Count() % 2))
                            theRounds[i].roundOrder[j - 1] = thePlayers.Count() / 2 + j;
                        else
                            theRounds[i].roundOrder[j - 1] = thePlayers.Count() / 2 - (thePlayers.Count() - j);
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 0)
                {
                    for (int j = 1; j <= thePlayers.Count(); j++)
                    {
                        if (j <= (thePlayers.Count() / 2))
                            theRounds[i].roundOrder[j - 1] = thePlayers.Count() / 2 - j + 1;
                        else
                            theRounds[i].roundOrder[j - 1] = thePlayers.Count() / 2 + (thePlayers.Count() - j) + 1;
                    }
                }
            }
            nowStatus.rounds = theRounds;

            int currentPickOrder = nowStatus.rounds[nowStatus.round - 1].roundOrder[nowStatus.currentOrderIndex];
            foreach (Player player in nowStatus.players)
            {
                if (player.initialPickOrder == currentPickOrder)
                {
                    nowStatus.currentPlayer = player;
                    break;
                }
            }

            Table bracketsTable = Table.LoadTable(dynamoDBClient, GetTableName("brackets"));
            var bracketsScanRequest = new ScanOperationConfig();
            var bracketsTableScan = bracketsTable.Scan(bracketsScanRequest);
            List<Document> bracketsDocuments = await bracketsTableScan.GetRemainingAsync();
            nowStatus.brackets = new Dictionary<int, string>();
            foreach (Document bracketDocument in bracketsDocuments)
            {
                nowStatus.brackets.Add(bracketDocument["bracket-id"].AsInt(), bracketDocument["name"].AsString());
            }

            return nowStatus;
        }

        public static async Task<Team[]> loadTeamsForBracketView(int bracketId)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            ScanFilter teamsFilter = new ScanFilter();
            teamsFilter.AddCondition("bracket-id", ScanOperator.Equal, bracketId.ToString());

            var teamsTableScan = teamsTable.Scan(teamsFilter);
            var teamsRetrieved = teamsTableScan.GetRemainingAsync().Result;
            List<Team> teams = new List<Team>();

            foreach (Document teamDocument in teamsRetrieved)
            {
                teams.Add(new Team()
                {
                    sCurveRank = teamDocument["s-curve-rank"].AsInt(),
                    teamName = teamDocument["team-name"].AsString(),
                    bracketId = teamDocument["bracket-id"].AsInt(),
                    eliminated = teamDocument["eliminated"].AsBoolean(),
                    rank = teamDocument["team-rank"].AsInt(),
                    wins = teamDocument["team-wins"].AsInt(),
                    losses = teamDocument["team-losses"].AsInt(),
                    cost = teamDocument["cost"].AsInt(),
                    pickedByPlayer = teamDocument["selected-by"].AsInt()
                });
            }

            return teams.ToArray();
        }

        public static string loadBracketName(int bracketId)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table bracketsTable = Table.LoadTable(dynamoDBClient, GetTableName("brackets"));
            var bracketsTableConfig = new GetItemOperationConfig()
            {
                AttributesToGet = new List<string> { "name" }
            };
            var bracketsTableRow = bracketsTable.GetItemAsync(bracketId, bracketsTableConfig).Result;

            return bracketsTableRow["name"].AsString();
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

        public static async Task<Team[]> loadTeams(params int[] sCurveRanks)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            ScanFilter teamsFilter = new ScanFilter();
            List<AttributeValue> attributeValues = new List<AttributeValue>();
            foreach (int sCurveRank in sCurveRanks)
            {
                attributeValues.Add(new AttributeValue(sCurveRank.ToString()));
            }
            teamsFilter.AddCondition("s-curve-rank", ScanOperator.In, attributeValues);

            var teamsTableScan = teamsTable.Scan(teamsFilter);
            var teamsRetrieved = teamsTableScan.GetRemainingAsync().Result;
            List<Team> teams = new List<Team>();

            foreach (Document teamDocument in teamsRetrieved)
            {
                teams.Add(new Team()
                {
                    sCurveRank = teamDocument["s-curve-rank"].AsInt(),
                    teamName = teamDocument["team-name"].AsString(),
                    bracketId = teamDocument["bracket-id"].AsInt(),
                    eliminated = teamDocument["eliminated"].AsBoolean(),
                    rank = teamDocument["team-rank"].AsInt(),
                    wins = teamDocument["team-wins"].AsInt(),
                    losses = teamDocument["team-losses"].AsInt(),
                    cost = teamDocument["cost"].AsInt(),
                    pickedByPlayer = teamDocument["selected-by"].AsInt()
                });
            }

            return teams.ToArray();
        }

        public static void advanceDraft(Team[] selectedTeams, CurrentStatus nowStatus, int pointsHeldOver)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));

            StringBuilder logStringBuilder = new StringBuilder(nowStatus.log);
            logStringBuilder.AppendFormat("<b>Round {0} - {1}:</b> ", nowStatus.round.ToString(), nowStatus.currentPlayer.name);

            List<TransactWriteItem> writeItems = new List<TransactWriteItem>();

            bool first = true;

            foreach (Team thisTeam in selectedTeams)
            {
                TransactWriteItem updateTeam = new TransactWriteItem();
                updateTeam.Update = new Update();
                updateTeam.Update.TableName = teamsTable.TableName;
                updateTeam.Update.Key.Add("s-curve-rank", new AttributeValue() { S = thisTeam.sCurveRank.ToString() });
                updateTeam.Update.ExpressionAttributeNames.Add("#selected_by", "selected-by");
                updateTeam.Update.ExpressionAttributeValues.Add(":selected_by", new AttributeValue() { S = nowStatus.currentPlayer.userId.ToString() });
                updateTeam.Update.UpdateExpression = "SET #selected_by = :selected_by";
                writeItems.Add(updateTeam);

                if (!first) logStringBuilder.Append(", ");
                logStringBuilder.Append(thisTeam.teamName + " (" + thisTeam.rank.ToString() + ", " + nowStatus.brackets[thisTeam.bracketId] + ", " + (thisTeam.cost).ToString() + " points)");
                first = false;
            }

            logStringBuilder.Append(" <i>Left " + pointsHeldOver.ToString() + " points</i><br />");
            int newOrderIndex;
            int newRound = nowStatus.round;
            if (nowStatus.currentOrderIndex >= nowStatus.players.Length - 1)
            {
                newOrderIndex = 0;
                newRound = nowStatus.round + 1;
                logStringBuilder.Append("<br />");
            }
            else
            {
                newOrderIndex = nowStatus.currentOrderIndex + 1;
            }

            Table statusTable = Table.LoadTable(dynamoDBClient, GetTableName("draftstatus"));
            TransactWriteItem updateDraftStatus = new TransactWriteItem();
            updateDraftStatus.Update = new Update();
            updateDraftStatus.Update.TableName = statusTable.TableName;
            updateDraftStatus.Update.Key.Add("draft-id", new AttributeValue() { N = "1" });
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#order_index", "order-index");
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#current_round", "current-round");
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#log", "log");
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":old_order_index", new AttributeValue() { N = nowStatus.currentOrderIndex.ToString() });
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":old_round", new AttributeValue() { N = nowStatus.round.ToString() });
            updateDraftStatus.Update.ConditionExpression = "#order_index = :old_order_index AND #current_round = :old_round";
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":log", new AttributeValue() { S = logStringBuilder.ToString() });
            if (nowStatus.round <= nowStatus.rounds.Length)
            {
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":new_order_index", new AttributeValue() { N = newOrderIndex.ToString() });
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":new_round", new AttributeValue() { N = newRound.ToString() });
                updateDraftStatus.Update.UpdateExpression = "SET #order_index = :new_order_index, #current_round = :new_round, #log = :log";
            }
            else
            {
                updateDraftStatus.Update.UpdateExpression = "SET finished = TRUE, #log = :log";
            }
            writeItems.Add(updateDraftStatus);

            Table userTable = Table.LoadTable(dynamoDBClient, GetTableName("users"));
            TransactWriteItem updateUser = new TransactWriteItem();
            updateUser.Update = new Update();
            updateUser.Update.TableName = userTable.TableName;
            updateUser.Update.Key.Add("user-id", new AttributeValue() { S = nowStatus.currentPlayer.userId.ToString() });
            updateUser.Update.ExpressionAttributeNames.Add("#points_held_over", "points-held-over");
            updateUser.Update.ExpressionAttributeValues.Add(":points_held_over", new AttributeValue() { N = pointsHeldOver.ToString() });
            updateUser.Update.UpdateExpression = "SET #points_held_over = :points_held_over";
            writeItems.Add(updateUser);

            TransactWriteItemsRequest transactWriteItemsRequest = new TransactWriteItemsRequest();
            transactWriteItemsRequest.TransactItems = writeItems;
            var transactionResult = dynamoDBClient.TransactWriteItemsAsync(transactWriteItemsRequest).Result;

            if (newRound > nowStatus.rounds.Length)
            {
                //runEndOfDraftLottery();
            }

        }

        /*
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


                */
    }
}
