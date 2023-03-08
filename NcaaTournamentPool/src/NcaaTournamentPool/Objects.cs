namespace NcaaTournamentPool
{

	public class Team
	{
        public int sCurveRank;
		public string teamName;
		public int wins;
		public int losses;
		public bool eliminated;
		public int rank;
        public int cost;
		public int bracketId;
		public int pickedByPlayer;
	}

	public class Player
	{
		public string name;
		public int userId;
		public string color;
		public int pointsAvailable;
		public int pointsHeldOver;
		public int initialPickOrder;
	}

	public class CurrentStatus
	{
		public int round;
		public int currentUserId
		{
			get
			{
				return currentPlayer != null ? currentPlayer.userId : 0;
			}
		}
		public int currentOrderIndex;
		public int maxHoldOverPoints
		{
			get
			{
				return currentRound != null ? currentRound.maxHoldOverPoints : 0;
			}
		}
		public int pointsToSpend
		{
			get
			{
				return currentRound != null ? currentRound.pointsToSpend : 0;
			}
		}
		public bool finished;
		public Player[] players;
		public Round[] rounds;
		public Dictionary<int, string> brackets;
		public Player currentPlayer;
		public Round currentRound;
		public string log;
	}

	public class Round
	{
		public int roundNumber;
		public int[] roundOrder;
		public int pointsToSpend;
		public int maxHoldOverPoints;
	}
}
