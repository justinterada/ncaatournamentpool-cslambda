using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NcaaTournamentPool.Pages
{
	public class EliminateTeamsModel : PageModel
    {
        public CurrentStatus CurrentStatus { get; set; }

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

        private Dictionary<int, Player> _players;
        public Dictionary<int, Player> Players
        {
            get
            {
                return _players;
            }
        }

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

            // Load up the players
            _players = new Dictionary<int, Player>();
            foreach (Player player in CurrentStatus.players)
            {
                _players.Add(player.userId, player);
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

            _allTeamsArray = allTeams.ToArray();
        }
    }
}
