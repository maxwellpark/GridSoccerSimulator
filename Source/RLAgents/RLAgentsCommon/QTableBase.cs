﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GridSoccer.Common;
using System.IO;

namespace GridSoccer.RLAgentsCommon
{
    public abstract class QTableBase
    {
        protected EligibilityTrace m_eTrace = null;
        protected State m_curState;
        protected State m_prevState;

        public QTableBase()
        {
            if (Params.RLMethod != Params.RLMethods.Q_Zero && Params.RLMethod != Params.RLMethods.SARSA_Zero)
                m_eTrace = new EligibilityTrace();
        }

        protected abstract double GetQValue(State s, int ai);

        protected abstract void UpdateQValue(State s, int ai, double newValue);

        protected abstract int MyUnum { get; }

        protected abstract int TeammatesCount { get; }

        public abstract void Save(TextWriter tw);

        public abstract void Load(TextReader tr);
        
        public abstract Array QTableArray { get; }

        public virtual void SetCurrentState(State s)
        {
            m_prevState = m_curState;
            m_curState = DicomposeState(s);
        }

        protected virtual State DicomposeState(State s)
        {
            return s;
        }

        public int GetCurrentGreedyActionIndex()
        {
            double dummy;
            return GetCurStateMaxQ(out dummy);
        }

        //public int GetGreedyActionIndex(State s)
        //{
        //    double dummy;
        //    return GetMaxQ(s, out dummy);
        //}

        public virtual int GetCurStateMaxQ(out double maxQValue)
        {
            return GetMaxQ(m_curState, out maxQValue);
        }

        private int GetMaxQ(State s, out double maxQValue)
        {
            double curValue = 0.0;
            maxQValue = Double.MinValue;
            int maxIndex = 0;

            int count = SoccerAction.GetActionCount(Params.MoveKings, TeammatesCount);
            for (int i = 0; i < count; ++i)
            {
                curValue = GetQValue(s, i);
                if (curValue > maxQValue)
                {
                    maxQValue = curValue;
                    maxIndex = i;
                }
            }

            return maxIndex;
        }

        protected double GetQValue(State s, SoccerAction a)
        {
            int aIndex = SoccerAction.GetIndexFromAction(a, Params.MoveKings, MyUnum);
            return GetQValue(s, aIndex);
        }

        protected void UpdateQValue(State s, SoccerAction a, double newValue)
        {
            int aIndex = SoccerAction.GetIndexFromAction(a, Params.MoveKings, MyUnum);
            UpdateQValue(s, aIndex, newValue);
        }

        protected void UpdateQ_SARSA(double reward, State prevState, State curState, int prevActIndex, int curActIndex)
        {
            double oldQ = GetQValue(prevState, prevActIndex);
            double qOfNewState = GetQValue(curState, curActIndex);
            double newQ = oldQ + Params.Alpha * (reward + Params.Gamma * qOfNewState - oldQ);
            UpdateQValue(prevState, prevActIndex, newQ);
        }

        protected void UpdateQ_QLearning(double reward, State prevState, State curState, int prevActIndex)
        {
            double maxQ = 0.0;
            GetMaxQ(curState, out maxQ);
            double oldQ = GetQValue(prevState, prevActIndex);
            double newQ = oldQ + Params.Alpha * (reward + Params.Gamma * maxQ - oldQ);
            UpdateQValue(prevState, prevActIndex, newQ);
        }

        protected void UpdateQ_QL_Watkins(double reward, State prevState, State curState, int prevActIndex, int curActIndex, bool isNaive)
        {
            double oldQ = GetQValue(prevState, prevActIndex);

            double maxQ = 0.0;
            GetMaxQ(curState, out maxQ);
            double delta = reward + Params.Gamma * maxQ - oldQ;

            bool isGreedy =
                GetCurrentGreedyActionIndex() == curActIndex;

            if (!m_eTrace.ContainsStateActionPair(prevState, prevActIndex))
                m_eTrace.AddStateActionPair(prevState, prevActIndex, 0.0);
            //else
            //    Console.WriteLine("Trace exists! " + prevState.GetStateString());

            if (Params.TraceType == Params.EligibilityTraceTypes.Accumulating)
            {
                m_eTrace.IncrementValue(prevState, prevActIndex);
            }
            else if (Params.TraceType == Params.EligibilityTraceTypes.Replacing)
            {
                m_eTrace.RemoveTracesForState(prevState, prevActIndex, SoccerAction.GetActionCount(Params.MoveKings, TeammatesCount));
                m_eTrace.UpdateValue(prevState, prevActIndex, 1.0);
            }

            var listTraceItems = m_eTrace.GetTraceItems().ToList();
            foreach (var pair in listTraceItems)
            {
                double q = GetQValue(pair.Key.State, pair.Key.ActionIndex);
                double e = pair.Value;
                UpdateQValue(pair.Key.State, pair.Key.ActionIndex,
                    q + Params.Alpha * delta * e);

                if (isGreedy)
                    m_eTrace.MultiplyValue(pair.Key.State, pair.Key.ActionIndex, Params.Gamma * Params.Lambda);
            }

            if (!isNaive & !isGreedy)
                m_eTrace.ClearTrace();

            // see if episode changed
            if (curState.OurScore != prevState.OurScore || curState.OppScore != prevState.OppScore)
                m_eTrace.ClearTrace();
        }

        protected void UpdateQ_SARSA_Lambda(double reward, State prevState, State curState, int prevActIndex, int curActIndex)
        {
            double oldQ = GetQValue(prevState, prevActIndex);
            double qOfNewState = GetQValue(curState, curActIndex);
            double delta = reward + Params.Gamma * qOfNewState - oldQ;

            if (!m_eTrace.ContainsStateActionPair(prevState, prevActIndex))
                m_eTrace.AddStateActionPair(prevState, prevActIndex, 0.0);
            else
                Console.WriteLine("Trace exists! " + prevState.GetStateString());

            if (Params.TraceType == Params.EligibilityTraceTypes.Accumulating)
            {
                m_eTrace.IncrementValue(prevState, prevActIndex);
            }
            else if (Params.TraceType == Params.EligibilityTraceTypes.Replacing)
            {
                m_eTrace.RemoveTracesForState(prevState, prevActIndex, SoccerAction.GetActionCount(Params.MoveKings, TeammatesCount));
                m_eTrace.UpdateValue(prevState, prevActIndex, 1.0);
            }

            var listTraceItems = m_eTrace.GetTraceItems().ToList();
            foreach (var pair in listTraceItems)
            {
                double q = GetQValue(pair.Key.State, pair.Key.ActionIndex);
                double e = pair.Value;
                UpdateQValue(pair.Key.State, pair.Key.ActionIndex,
                    q + Params.Alpha * delta * e);
                m_eTrace.MultiplyValue(pair.Key.State, pair.Key.ActionIndex, Params.Gamma * Params.Lambda);
            }

            // see if episode changed
            if (curState.OurScore != prevState.OurScore || curState.OppScore != prevState.OppScore)
                m_eTrace.ClearTrace();
        }


        public virtual void UpdateQ_SARSA(int prevActIndex, int curActIndex)
        {
            double reward = EnvironmentModeler.GetReward(m_prevState, m_curState, 
                SoccerAction.GetActionTypeFromIndex( prevActIndex,Params.MoveKings));
            UpdateQ_SARSA(reward, m_prevState, m_curState, prevActIndex, curActIndex);
        }

        public virtual void UpdateQ_QLearning(int prevActIndex)
        {
            double reward = EnvironmentModeler.GetReward(m_prevState, m_curState,
                SoccerAction.GetActionTypeFromIndex(prevActIndex, Params.MoveKings));
            UpdateQ_QLearning(reward, m_prevState, m_curState, prevActIndex);
        }

        public virtual void UpdateQ_QL_Watkins(int prevActIndex, int curActIndex, bool isNaive)
        {
            double reward = EnvironmentModeler.GetReward(m_prevState, m_curState,
                SoccerAction.GetActionTypeFromIndex(prevActIndex, Params.MoveKings));
            UpdateQ_QL_Watkins(reward, m_prevState, m_curState, prevActIndex, curActIndex, isNaive);
        }

        public virtual void UpdateQ_SARSA_Lambda(int prevActIndex, int curActIndex)
        {
            double reward = EnvironmentModeler.GetReward(m_prevState, m_curState,
                SoccerAction.GetActionTypeFromIndex(prevActIndex, Params.MoveKings));
            UpdateQ_SARSA_Lambda(reward, m_prevState, m_curState, prevActIndex, curActIndex);
        }
    }
}
