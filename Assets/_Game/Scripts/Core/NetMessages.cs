// World Crisis | Слой: Core | Ответственный: OpenCode
// Сетевые DTO: команды клиента авторитетному серверу и снимки состояния матча.
// Только публичные поля и примитивы — это требование JsonUtility.

using System;
using System.Collections.Generic;

namespace WorldCrisis
{
    /// <summary>Роль процесса в матче: одиночка, хост-сервер или клиент.</summary>
    public enum NetRole
    {
        Solo = 0,
        Host = 1,
        Client = 2
    }

    /// <summary>Операции командного канала (клиент → хост и хост → клиент).</summary>
    public enum NetOp
    {
        None = 0,
        Hello = 1,
        AssignSlot = 2,
        NoFreeSlots = 3,
        SetLocalPlayer = 4,
        StartMatch = 5,
        EndPhaseEarly = 6,
        ForceAdvance = 7,
        TransferMoney = 8,
        IssueAirCredit = 9,
        BuyRecon = 10,
        BuyWiretap = 11,
        PledgeEcology = 12,
        PlacePhoneCall = 13,
        OfferPact = 14,
        RespondToPactOffer = 15,
        CastSanctionVote = 16,
        SignPact = 17,
        BreakPact = 18,
        SubmitOrder = 19,
        CancelOrder = 20,
        ClearOrders = 21
    }

    /// <summary>Конверт команды. Поле Kind всегда "cmd" — отличает команду от снимка состояния.</summary>
    [Serializable]
    public class NetMessage
    {
        public string Kind = NetKinds.Command;
        public NetOp Op;
        public int Actor;
        public int A;
        public int B;
        public int C;
        public float F;
        public string S;

        public static NetMessage Create(NetOp op, CountryId actor)
        {
            NetMessage message = new NetMessage();
            message.Op = op;
            message.Actor = (int)actor;
            return message;
        }
    }

    public static class NetKinds
    {
        public const string Command = "cmd";
        public const string Snapshot = "snap";
        public const int SnapshotVersion = 1;
    }

    [Serializable]
    public class CitySnapshot
    {
        public int Index;
        public bool Destroyed;
        public int Development;
        public bool Shield;
    }

    /// <summary>Снимок одной державы. Доктрина передаётся только локальному игроку — чужие тайны не утекают.</summary>
    [Serializable]
    public class PlayerSnapshot
    {
        public int Country;
        public int Money;
        public int Stock;
        public bool Defeated;
        public bool Ai;
        public bool Traitor;
        public bool Sanctioned;
        public bool Pledged;
        public bool DoctrineDone;
        public int Doctrine = -1;
        public List<CitySnapshot> Cities = new List<CitySnapshot>();
        public List<int> Pacts = new List<int>();
    }

    [Serializable]
    public class JournalSnapshot
    {
        public string Text;
        public int Tone;
        public int Round;
    }

    /// <summary>Полный снимок матча, который хост рассылает клиентам.</summary>
    [Serializable]
    public class MatchSnapshot
    {
        public string Kind = NetKinds.Snapshot;
        public int Version = NetKinds.SnapshotVersion;
        public int Round;
        public int Phase;
        public float Time;
        public float Ecology;
        public int CitiesDestroyed;
        public int LocalCountry;
        public int Winner = -1;
        public string Reason;
        public List<PlayerSnapshot> Players = new List<PlayerSnapshot>();
        public List<JournalSnapshot> Journal = new List<JournalSnapshot>();
    }
}