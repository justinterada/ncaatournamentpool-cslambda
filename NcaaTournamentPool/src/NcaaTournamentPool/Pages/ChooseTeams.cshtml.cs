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
        public Player CurrentPlayer { get; set; }
        public Round[] Rounds { get; set; }

        public int UserId { get; set; }
        public string BracketOneName { get; set; }
        public string BracketTwoName { get; set; }
        public string BracketThreeName { get; set; }
        public string BracketFourName { get; set; }

        private Dictionary<int, Player> _players;
        public Dictionary<int, Player> Players
        {
            get
            {
                return _players;
            }
        }

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
        public Team[] AllTeamsArray
        {
            get
            {
                return _allTeamsArray;
            }
        }

        public void OnGet()
        {
            CurrentStatus = CommonMethods.loadCurrentStatus().Result;
            UserId = Convert.ToInt32(this.Request.Query["userId"]);

            Rounds = CurrentStatus.rounds;
            CurrentRound = CurrentStatus.currentRound;

            int currentPickOrder = Rounds[CurrentStatus.round - 1].roundOrder[CurrentStatus.currentOrderIndex];

            // Load up the players
            _players = new Dictionary<int, Player>();
            foreach (Player player in CurrentStatus.players)
            {
                _players.Add(player.userId, player);
            }
            CurrentPlayer = CurrentStatus.currentPlayer;

            if (CurrentStatus.currentUserId != UserId)
            {
                Response.Redirect(Url.Content(string.Format("~/Lobby?userId={0}", UserId)));
            }

            List<Team> allTeams = new List<Team>();

            Team[] teams = CommonMethods.loadTeamsForBracketView(1);
            _bracketOneTeams = CommonMethods.SortTeamsByRank(teams);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(2);
            _bracketTwoTeams = CommonMethods.SortTeamsByRank(teams);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(3);
            _bracketThreeTeams = CommonMethods.SortTeamsByRank(teams);
            allTeams.AddRange(teams);

            teams = CommonMethods.loadTeamsForBracketView(4);
            _bracketFourTeams = CommonMethods.SortTeamsByRank(teams);
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
