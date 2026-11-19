using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public interface IWordboundLobbyParty
    {
        bool Active { get; }
        bool Leader { get; }
        bool Searching { get; }
        bool Found { get; }
        string Heading { get; }
        string Status { get; }
        string Problem { get; }
        string SearchTitle { get; }
        IReadOnlyList<string> Members { get; }
        bool Copied { get; }
        bool CanCopy { get; }
        bool CanLeave { get; }
        bool CanFind { get; }
        bool CanCancel { get; }
        bool CanRetry { get; }
        void Invite();
        void Join(string code);
        void Copy();
        void Leave();
        void FindMatch();
        void Cancel();
        void Retry();
    }

    public static class WordboundLobbyParty
    {
        static Func<IWordboundLobbyParty> factory;
        static IWordboundLobbyParty current;
        static string message;

        public static bool Available => factory != null;

        public static IWordboundLobbyParty Current
        {
            get
            {
                if (current is UnityEngine.Object owner && !owner) current = null;
                return current;
            }
        }

        public static bool InParty => Current?.Active == true;

        public static Func<IWordboundLobbyParty> Register(Func<IWordboundLobbyParty> create)
        {
            var previous = factory;
            factory = create;
            return previous;
        }

        public static IWordboundLobbyParty Open()
        {
            if (Current != null || factory == null) return Current;
            try { current = factory(); }
            catch (Exception error)
            {
                current = null;
                Debug.LogWarning("[Wreckabulary] The lobby party could not start: " + error.Message);
            }
            return Current;
        }

        public static void Closed(IWordboundLobbyParty party, string reason = null)
        {
            if (party == null || !ReferenceEquals(current, party)) return;
            current = null;
            if (!string.IsNullOrWhiteSpace(reason)) message = reason;
        }

        public static string TakeMessage()
        {
            string result = message;
            message = null;
            return result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            current = null;
            message = null;
        }
    }
}
