using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
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

        public static void resetDraft()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);

            List<TransactWriteItem> writeItems = new List<TransactWriteItem>();

            Table statusTable = Table.LoadTable(dynamoDBClient, GetTableName("draftstatus"));
            TransactWriteItem updateDraftStatus = new TransactWriteItem();
            updateDraftStatus.Update = new Update();
            updateDraftStatus.Update.TableName = statusTable.TableName;
            updateDraftStatus.Update.Key.Add("draft-id", new AttributeValue() { N = "1" });
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#order_index", "order-index");
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#current_round", "current-round");
            updateDraftStatus.Update.ExpressionAttributeNames.Add("#log", "log");
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":false", new AttributeValue() { BOOL = false});
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":order_index", new AttributeValue() { N = "0" });
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":round", new AttributeValue() { N = "1" });
            updateDraftStatus.Update.ExpressionAttributeValues.Add(":log", new AttributeValue() { S = string.Empty });
            updateDraftStatus.Update.UpdateExpression = "SET finished = :false, #order_index = :order_index, #current_round = :round, #log = :log";
            writeItems.Add(updateDraftStatus);

            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            foreach (Team team in loadAllTeams().Result)
            {
                TransactWriteItem updateTeam = new TransactWriteItem();
                updateTeam.Update = new Update();
                updateTeam.Update.TableName = teamsTable.TableName;
                updateTeam.Update.Key.Add("s-curve-rank", new AttributeValue() { S = team.sCurveRank.ToString() });
                updateTeam.Update.ExpressionAttributeNames.Add("#selected_by", "selected-by");
                updateTeam.Update.ExpressionAttributeValues.Add(":selected_by", new AttributeValue() { S = "0" });
                updateTeam.Update.ExpressionAttributeValues.Add(":false", new AttributeValue() { BOOL = false });
                updateTeam.Update.UpdateExpression = "SET #selected_by = :selected_by, eliminated = :false";
                writeItems.Add(updateTeam);

            }

            Table userTable = Table.LoadTable(dynamoDBClient, GetTableName("users"));
            foreach (Player player in loadPlayers(dynamoDBClient).Result)
            {
                TransactWriteItem updateUser = new TransactWriteItem();
                updateUser.Update = new Update();
                updateUser.Update.TableName = userTable.TableName;
                updateUser.Update.Key.Add("user-id", new AttributeValue() { S = player.userId.ToString() });
                updateUser.Update.ExpressionAttributeNames.Add("#points_held_over", "points-held-over");
                updateUser.Update.ExpressionAttributeValues.Add(":points_held_over", new AttributeValue() { N = "0" });
                updateUser.Update.UpdateExpression = "SET #points_held_over = :points_held_over";
                writeItems.Add(updateUser);
            }

            TransactWriteItemsRequest transactWriteItemsRequest = new TransactWriteItemsRequest();
            transactWriteItemsRequest.TransactItems = writeItems;
            _ = dynamoDBClient.TransactWriteItemsAsync(transactWriteItemsRequest).Result;
        }

        private static string GetTableName(string table)
        {
            return string.Format("ncaatournamentpool-{0}", table);
        }

        public static async Task<CurrentStatus> loadCurrentStatus()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table statusTable = Table.LoadTable(dynamoDBClient, GetTableName("draftstatus"));
            Table roundTable = Table.LoadTable(dynamoDBClient, GetTableName("rounds"));

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

            nowStatus.players = loadPlayers(dynamoDBClient).Result;

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

                theRounds[i].roundOrder = new int[nowStatus.players.Length];

                if ((theRounds[i].roundNumber % 4) == 1)
                {
                    for (int j = 1; j <= nowStatus.players.Length; j++)
                    {
                        theRounds[i].roundOrder[j - 1] = j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 2)
                {
                    for (int j = 1; j <= nowStatus.players.Length; j++)
                    {
                        theRounds[i].roundOrder[j - 1] = nowStatus.players.Length + 1 - j;
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 3)
                {
                    for (int j = 1; j <= nowStatus.players.Length; j++)
                    {
                        if (j <= (nowStatus.players.Length / 2) + (nowStatus.players.Length % 2))
                            theRounds[i].roundOrder[j - 1] = nowStatus.players.Length / 2 + j;
                        else
                            theRounds[i].roundOrder[j - 1] = nowStatus.players.Length / 2 - (nowStatus.players.Length - j);
                    }
                }

                if ((theRounds[i].roundNumber % 4) == 0)
                {
                    for (int j = 1; j <= nowStatus.players.Length; j++)
                    {
                        if (j <= (nowStatus.players.Length / 2))
                            theRounds[i].roundOrder[j - 1] = nowStatus.players.Length / 2 - j + 1;
                        else
                            theRounds[i].roundOrder[j - 1] = nowStatus.players.Length / 2 + (nowStatus.players.Length - j) + 1;
                    }
                }
            }
            nowStatus.rounds = theRounds;

            if (nowStatus.currentOrderIndex >= 0 && nowStatus.currentOrderIndex < nowStatus.players.Length && nowStatus.round > 0 && nowStatus.round <= nowStatus.rounds.Length)
            {
                int currentPickOrder = nowStatus.rounds[nowStatus.round - 1].roundOrder[nowStatus.currentOrderIndex];
                foreach (Player player in nowStatus.players)
                {
                    if (player.initialPickOrder == currentPickOrder)
                    {
                        nowStatus.currentPlayer = player;
                        break;
                    }
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

        private static async Task<Player[]> loadPlayers(AmazonDynamoDBClient dynamoDBClient)
        {
            Table userTable = Table.LoadTable(dynamoDBClient, GetTableName("users"));
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
            return thePlayers.ToArray();
        }

        public static Team[] loadTeamsForBracketView(int bracketId)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            ScanFilter teamsFilter = new ScanFilter();
            teamsFilter.AddCondition("bracket-id", ScanOperator.Equal, bracketId.ToString());

            var teamsTableScan = teamsTable.Scan(teamsFilter);
            var teamsRetrieved = teamsTableScan.GetRemainingAsync().Result;
            teamsRetrieved.Sort((x, y) => x["s-curve-rank"].AsInt().CompareTo(y["s-curve-rank"].AsInt()));
            List<Team> teams = new List<Team>();
            foreach (Document teamDocument in teamsRetrieved)
            {
                teams.Add(new Team(teamDocument));
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
            var teamsRetrieved = await teamsTableScan.GetRemainingAsync();
            teamsRetrieved.Sort((x, y) => x["s-curve-rank"].AsInt().CompareTo(y["s-curve-rank"].AsInt()));
            List<Team> teams = new List<Team>();
            foreach (Document teamDocument in teamsRetrieved)
            {
                teams.Add(new Team(teamDocument));
            }

            return teams.ToArray();
        }

        public static async Task<Team[]> loadAllTeams()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            ScanOperationConfig scanConfig = new ScanOperationConfig();
            List<AttributeValue> attributeValues = new List<AttributeValue>();

            var teamsTableScan = teamsTable.Scan(scanConfig);
            var teamsRetrieved = await teamsTableScan.GetRemainingAsync();
            teamsRetrieved.Sort((x, y) => x["s-curve-rank"].AsInt().CompareTo(y["s-curve-rank"].AsInt()));
            List<Team> teams = new List<Team>();
            foreach (Document teamDocument in teamsRetrieved)
            {
                teams.Add(new Team(teamDocument));
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
            if (newRound <= nowStatus.rounds.Length)
            {
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":new_order_index", new AttributeValue() { N = newOrderIndex.ToString() });
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":new_round", new AttributeValue() { N = newRound.ToString() });
                updateDraftStatus.Update.UpdateExpression = "SET #order_index = :new_order_index, #current_round = :new_round, #log = :log";
            }
            else
            {
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":true", new AttributeValue() { BOOL = true });
                updateDraftStatus.Update.UpdateExpression = "SET finished = :true, #log = :log";
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
            _ = dynamoDBClient.TransactWriteItemsAsync(transactWriteItemsRequest).Result;

            if (newRound > nowStatus.rounds.Length)
            {
                runEndOfDraftLottery();
            }

        }

        public static void runEndOfDraftLottery()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            Team[] theTeams = loadTeamsForLotteryAsTeams();

            if (theTeams.Length > 0)
            {
                List<TransactWriteItem> writeItems = new List<TransactWriteItem>();

                CurrentStatus currentStatus = loadCurrentStatus().Result;
                StringBuilder logStringBuilder = new StringBuilder(currentStatus.log);
                logStringBuilder.Append("<b>End of Draft Lottery</b><br />");

                int totalEntries = 0;
                int entriesToDeduct = 1;

                foreach (Player thisPlayer in currentStatus.players)
                {
                    // Adjust for the 2011 doubling of points
                    thisPlayer.pointsHeldOver = Convert.ToInt32(Math.Floor(thisPlayer.pointsHeldOver / 2.0));

                    totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                }

                if (totalEntries == 0)
                {
                    foreach (Player thisPlayer in currentStatus.players)
                    {
                        thisPlayer.pointsHeldOver = 1;
                        totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                    }
                }

                foreach (Player thisPlayer in currentStatus.players)
                {
                    logStringBuilder.Append("<b>" + thisPlayer.name + "</b> has <b>" + thisPlayer.pointsHeldOver.ToString() + "</b> entries.<br />");
                }

                logStringBuilder.Append("<br />");

                while (totalEntries < theTeams.Length)
                {
                    totalEntries = 0;
                    foreach (Player thisPlayer in currentStatus.players)
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
                            foreach (Player thisPlayer in currentStatus.players)
                            {
                                thisPlayer.pointsHeldOver = 1;
                                totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                                entriesToDeduct = 1;
                            }
                        }
                        else
                        {
                            totalEntries = 0;
                            foreach (Player thisPlayer in currentStatus.players)
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

                    foreach (Player thisPlayer in currentStatus.players)
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

                    TransactWriteItem updateTeam = new TransactWriteItem();
                    updateTeam.Update = new Update();
                    updateTeam.Update.TableName = teamsTable.TableName;
                    updateTeam.Update.Key.Add("s-curve-rank", new AttributeValue() { S = thisTeam.sCurveRank.ToString() });
                    updateTeam.Update.ExpressionAttributeNames.Add("#selected_by", "selected-by");
                    updateTeam.Update.ExpressionAttributeValues.Add(":selected_by", new AttributeValue() { S = currentStatus.players[givenTo].userId.ToString() });
                    updateTeam.Update.UpdateExpression = "SET #selected_by = :selected_by";
                    writeItems.Add(updateTeam);

                    logStringBuilder.Append(thisTeam.teamName + " (" + thisTeam.rank.ToString() + ", " + currentStatus.brackets[thisTeam.bracketId] + ", " + (thisTeam.cost).ToString() + " points) ");
                    logStringBuilder.Append("is awarded to <b>" + currentStatus.players[givenTo].name + "</b>.<br />");

                    currentStatus.players[givenTo].pointsHeldOver = currentStatus.players[givenTo].pointsHeldOver - entriesToDeduct;

                    totalEntries = 0;

                    foreach (Player thisPlayer in currentStatus.players)
                    {
                        totalEntries = totalEntries + thisPlayer.pointsHeldOver;
                    }

                    teamsDone++;
                }

                Table statusTable = Table.LoadTable(dynamoDBClient, GetTableName("draftstatus"));
                TransactWriteItem updateDraftStatus = new TransactWriteItem();
                updateDraftStatus.Update = new Update();
                updateDraftStatus.Update.TableName = statusTable.TableName;
                updateDraftStatus.Update.Key.Add("draft-id", new AttributeValue() { N = "1" });
                updateDraftStatus.Update.ExpressionAttributeNames.Add("#order_index", "order-index");
                updateDraftStatus.Update.ExpressionAttributeNames.Add("#current_round", "current-round");
                updateDraftStatus.Update.ExpressionAttributeNames.Add("#log", "log");
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":old_order_index", new AttributeValue() { N = currentStatus.currentOrderIndex.ToString() });
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":old_round", new AttributeValue() { N = currentStatus.round.ToString() });
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":negative_one", new AttributeValue() { N = "-1" });
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":true", new AttributeValue() { BOOL = true });
                updateDraftStatus.Update.ConditionExpression = "#order_index = :old_order_index AND #current_round = :old_round";
                updateDraftStatus.Update.ExpressionAttributeValues.Add(":log", new AttributeValue() { S = logStringBuilder.ToString() });
                updateDraftStatus.Update.UpdateExpression = "SET finished = :true, #log = :log, #order_index = :negative_one, #current_round = :negative_one";
                writeItems.Add(updateDraftStatus);

                TransactWriteItemsRequest transactWriteItemsRequest = new TransactWriteItemsRequest();
                transactWriteItemsRequest.TransactItems = writeItems;
                _ = dynamoDBClient.TransactWriteItemsAsync(transactWriteItemsRequest).Result;
            }
        }

        public static Team[] loadTeamsForLotteryAsTeams()
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            ScanFilter teamsFilter = new ScanFilter();
            teamsFilter.AddCondition("selected-by", ScanOperator.Equal, "0");

            var teamsTableScan = teamsTable.Scan(teamsFilter);
            var teamsRetrieved = teamsTableScan.GetRemainingAsync().Result;
            teamsRetrieved.Sort((x, y) => x["s-curve-rank"].AsInt().CompareTo(y["s-curve-rank"].AsInt()));
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

        public static void setEliminatedTeams(int[] sCurveRanks)
        {
            AmazonDynamoDBClient dynamoDBClient = new AmazonDynamoDBClient(dynamoDBConfig);

            HashSet<int> sCurveRankSet = new HashSet<int>();
            foreach (int eliminatedTeam in sCurveRanks) {
                sCurveRankSet.Add(eliminatedTeam);
            }

            List<TransactWriteItem> writeItems = new List<TransactWriteItem>();
            Table teamsTable = Table.LoadTable(dynamoDBClient, GetTableName("teams"));
            foreach (Team team in loadAllTeams().Result)
            {
                TransactWriteItem updateTeam = new TransactWriteItem();
                updateTeam.Update = new Update();
                updateTeam.Update.TableName = teamsTable.TableName;
                updateTeam.Update.Key.Add("s-curve-rank", new AttributeValue() { S = team.sCurveRank.ToString() });
                updateTeam.Update.ExpressionAttributeValues.Add(":eliminated", new AttributeValue() { BOOL = sCurveRankSet.Contains(team.sCurveRank) });
                updateTeam.Update.UpdateExpression = "SET eliminated = :eliminated";
                writeItems.Add(updateTeam);
            }

            TransactWriteItemsRequest transactWriteItemsRequest = new TransactWriteItemsRequest();
            transactWriteItemsRequest.TransactItems = writeItems;
            _ = dynamoDBClient.TransactWriteItemsAsync(transactWriteItemsRequest).Result;
        }
    }
}
