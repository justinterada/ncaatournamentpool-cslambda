using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NcaaTournamentPool.Pages
{
	public class BracketViewModel : PageModel
    {
        private CurrentStatus _currentStatus;
        public CurrentStatus CurrentStatus
        {
            get
            {
                return _currentStatus;
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

        private Dictionary<int, string> _playerColorsByUserId;
        public Dictionary<int, string> PlayerColorsByUserId
        { 
            get
            {
                return _playerColorsByUserId;
	        }
        }

        public void OnGet()
        {
            _currentStatus = CommonMethods.loadCurrentStatus().Result;
            _playerColorsByUserId = new Dictionary<int, string>();
            foreach (Player player in _currentStatus.players)
            {
                _playerColorsByUserId.Add(player.userId, player.color);
            }

            Team[] teams = CommonMethods.loadTeamsForBracketView(1);
            _bracketOneTeams = CommonMethods.SortTeamsByRank(teams);

            teams = CommonMethods.loadTeamsForBracketView(2);
            _bracketTwoTeams = CommonMethods.SortTeamsByRank(teams);

            teams = CommonMethods.loadTeamsForBracketView(3);
            _bracketThreeTeams = CommonMethods.SortTeamsByRank(teams);

            teams = CommonMethods.loadTeamsForBracketView(4);
            _bracketFourTeams = CommonMethods.SortTeamsByRank(teams);
        }
    }
}
