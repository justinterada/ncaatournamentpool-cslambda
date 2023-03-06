using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NcaaTournamentPool.Pages
{
	public class ChooseTeamsModel : PageModel
    {
        public CurrentStatus CurrentStatus { get; set; }
        public Round CurrentRound { get; set; }
        public Round[] Rounds { get; set; }

        public string UserId { get; set; }
        public string BracketOneName { get; set; }
        public string BracketTwoName { get; set; }
        public string BracketThreeName { get; set; }
        public string BracketFourName { get; set; }

        private Dictionary<int, Player> _players;

        private Dictionary<int, List<Team>> _bracketOneTeams;
        public Dictionary<int, List<Team>> BracketOneTeams
        {
            get
            {
                return _bracketOneTeams;
            }
        }

        private Dictionary<int, List<Team>> _bracketTwoTeams;
        public Dictionary<int, List<Team>> BracketTwoTeams
        {
            get
            {
                return _bracketTwoTeams;
            }
        }

        private Dictionary<int, List<Team>> _bracketThreeTeams;
        public Dictionary<int, List<Team>> BracketThreeTeams
        {
            get
            {
                return _bracketThreeTeams;
            }
        }

        private Dictionary<int, List<Team>> _bracketFourTeams;
        public Dictionary<int, List<Team>> BracketFourTeams
        {
            get
            {
                return _bracketFourTeams;
            }
        }


        private Dictionary<int, Team> _allTeamsBySCurveRank;
        private Team[] _allTeamsArray;

        public void OnGet()
        {
            CurrentStatus = CommonMethods.loadCurrentStatus().Result;
            UserId = this.Request.Query["userId"];

            Rounds = CommonMethods.loadRoundsForLobby().Result;
            CurrentRound = Rounds[CurrentStatus.round - 1];

            int currentPickOrder = Rounds[CurrentStatus.round - 1].roundOrder[CurrentStatus.currentOrderIndex];

            // Load up the players
            _players = new Dictionary<int, Player>();
            foreach (Player player in CommonMethods.loadPlayersForLobby().Result)
            {
                _players.Add(player.userId, player);

                if (player.initialPickOrder == currentPickOrder)
                {
                    CurrentStatus.currentUserId = player.userId;
                }
            }

            if (CurrentStatus.currentUserId.ToString() != UserId)
            {
                Response.Redirect(string.Format("lobby.aspx?userid={0}", UserId));
            }

            List<Team> allTeams = new List<Team>();

            Team[] teams = CommonMethods.loadTeamsForBracketView(1).Result;
            _bracketOneTeams = CommonMethods.SortTeamsByRank(teams);
            //BuildBracket(_bracketOneTeams, placeholderBracket1);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(2).Result;
            _bracketTwoTeams = CommonMethods.SortTeamsByRank(teams);
            //BuildBracket(_bracketTwoTeams, placeholderBracket2);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(3).Result;
            _bracketThreeTeams = CommonMethods.SortTeamsByRank(teams);
            //BuildBracket(_bracketThreeTeams, placeholderBracket3);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(4).Result;
            _bracketFourTeams = CommonMethods.SortTeamsByRank(teams);
            //BuildBracket(_bracketFourTeams, placeholderBracket4);
            allTeams.AddRange(teams);

            allTeams.Sort(delegate (Team l, Team r) { return l.sCurveRank.CompareTo(r.sCurveRank); });

            _allTeamsBySCurveRank = new Dictionary<int, Team>();
            foreach (Team team in allTeams)
            {
                _allTeamsBySCurveRank.Add(team.sCurveRank, team);
            }

            BracketOneName = CommonMethods.loadBracketName(1);
            BracketTwoName = CommonMethods.loadBracketName(2);
            BracketThreeName = CommonMethods.loadBracketName(3);
            BracketFourName = CommonMethods.loadBracketName(4);

            _allTeamsArray = allTeams.ToArray();

        }
    }
}
