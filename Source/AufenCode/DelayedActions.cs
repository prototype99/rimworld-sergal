using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AufenCode
{
    // Game-wide component to manage delayed actions
    public class DelayedActionsComponent : GameComponent
    {
        private struct Scheduled
        {
            public int tick;
            public Action action;
        }

        private readonly List<Scheduled> _queue = new List<Scheduled>();
        public static DelayedActionsComponent Instance { get; private set; }

        public DelayedActionsComponent(Game game) : base()
        {
            Instance = this;
        }

        public override void FinalizeInit()
        {
            // Ensure singleton assignment if deserialized
            Instance = this;
            base.FinalizeInit();
        }

        public override void GameComponentTick()
        {
            if (_queue.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            // Execute due actions; avoid allocations
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].tick <= now)
                {
                    try
                    {
                        _queue[i].action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[AufenCode] Exception in delayed action: {ex}");
                    }
                    _queue.RemoveAt(i);
                }
            }
        }

        public void Schedule(int delayTicks, Action action)
        {
            if (action == null) return;
            int due = Math.Max(0, delayTicks) + Find.TickManager.TicksGame;
            _queue.Add(new Scheduled { tick = due, action = action });
        }
    }

    public static class TickManagerExtensions
    {
        // Allows: Find.TickManager.DoLater(ticks, () => { ... });
        public static void DoLater(this TickManager tm, int delayTicks, Action action)
        {
            if (Current.Game == null)
            {
                // If no game context, run immediately to avoid null refs during load screens
                action?.Invoke();
                return;
            }
            if (DelayedActionsComponent.Instance == null)
            {
                // Attach our component if missing
                Current.Game.components.Add(new DelayedActionsComponent(Current.Game));
            }
            DelayedActionsComponent.Instance.Schedule(delayTicks, action);
        }
    }
}
