﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GridSoccer.NEATAgentsBase;
using GridSoccer.RLAgentsCommon;
using SharpNeatLib.NeuralNetwork;
using System.Xml;
using SharpNeatLib.NeatGenome;
using SharpNeatLib.NeatGenome.Xml;
using SharpNeatLib.Experiments;
using GridSoccer.Common;

namespace GridSoccer.HyperNEATControllerAgent.CCEA3LayersFieldSubs
{
    public class FieldSubs3LQTable : NeatQTableBase
    {
        private bool m_loadingBehaviour = false;
        private INetwork m_loadedNetwork = null;

        public FieldSubs3LQTable(RLClientBase client, int numTeammates, int myUnum)
            : base(client, numTeammates, myUnum, false)
        {
            if (Program.LoadingGenome)
            {
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(Program.LoadedGenomePath);
                    NeatGenome readCPPN = XmlNeatGenomeReaderStatic.Read(doc);

                    var substrate = new MultiLayerSandwichSubstrate(Rows, Cols + 2, NeatExpParams.SubstrateLayers,
                        NeatExpParams.AddBiasToSubstrate, HyperNEATParameters.substrateActivationFunction);

                    SharpNeatLib.NeatGenome.NeatGenome tempGenome = substrate.GenerateGenome(readCPPN.Decode(HyperNEATParameters.substrateActivationFunction));
                    m_loadedNetwork = tempGenome.Decode(HyperNEATParameters.substrateActivationFunction);

                    m_loadingBehaviour = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    m_loadingBehaviour = false;
                }
            }

        }

        public override IExperiment CreateExperiment()
        {
            // if the substrate contains bias nodes, then we should
            // have CPPNs with 2 outputs
            return new FieldSubs3LExperiment(this, 4,
                NeatExpParams.AddBiasToSubstrate ? 
                (NeatExpParams.SubstrateLayers - 1) * 2 
                : NeatExpParams.SubstrateLayers - 1);
        }

        public override double[] GetQValuesForNetwork(INetwork networkBallOwner, INetwork networkNotBallOwner, State s)
        {
            if (this.m_loadingBehaviour)
            {
                networkBallOwner = m_loadedNetwork;
                networkNotBallOwner = m_loadedNetwork;
            }


            INetwork networkToEval = networkBallOwner;

            //if (!s.AreWeBallOwner)
            //    networkToEval = networkNotBallOwner;

            networkToEval.ClearSignals();

            // No need to do this, it seems that ClearSignals sets all inputs to 0.0

            //for (int i = 0, len = network.InputNeuronCount; i < len; i++)
            //{
            //    network.SetInputSignals() = 0.0;
            //}

            // add info of players to the input signal array
            for (int oi = 0, oicount = s.OppPlayersList.Count; oi < oicount; oi++)
            {
                var pos = s.OppPlayersList[oi].Position;
                int signali = RowColToSignalIndex(pos.Row, pos.Col);
                double signalval = -.3;
                if (s.OppPlayersList[oi].IsBallOwner)
                    signalval -= 0.5;

                networkToEval.SetInputSignal(signali, signalval);
            }

            // add info for ours
            for (int ti = 0, ticount = s.OurPlayersList.Count; ti < ticount; ti++)
            {
                var pos = s.OurPlayersList[ti].Position;
                int signali = RowColToSignalIndex(pos.Row, pos.Col);
                double signalval = 0.3;
                if (s.OurPlayersList[ti].IsBallOwner)
                    signalval += 0.5;

                networkToEval.SetInputSignal(signali, signalval);
            }

            // add info for me
            do // just create a block
            {
                var pos = s.Me.Position;
                int signali = RowColToSignalIndex(pos.Row, pos.Col);
                // TODO: it was 0.5
                double signalval = 0.5;
                if (s.Me.IsBallOwner)
                    signalval += 0.5;

                networkToEval.SetInputSignal(signali, signalval);
            } while (false);


            //if(network.RelaxNetwork(2, 0.0))
            networkToEval.MultipleSteps(2);

            // See which position gets the maximum activation
            var maxOutput = Single.MinValue;
            int maxOutIndex = -1;
            for (int osi = 0, oscount = networkToEval.OutputNeuronCount; osi < oscount; osi++)
            {
                var curValue = networkToEval.GetOutputSignal(osi);
                if (curValue > maxOutput)
                {
                    maxOutput = curValue;
                    maxOutIndex = osi;
                }
            }

            // Now interpret the position of max activation
            int maxr, maxc;
            SignalIndexToRowCol(maxOutIndex, out maxr, out maxc);


            int hsAct = InterpretHighestSignal(s, maxr, maxc);

            // TOOD:
            // Console.WriteLine("HSACT: {0}", hsAct);

            double[] qValues = new double[m_numActions];
            qValues[hsAct] = 1.0;
            return qValues;
        }

       
        /// <summary>
        /// i is 0-based, but r and c are 1-based
        /// </summary>
        private void SignalIndexToRowCol(int i, out int r, out int c)
        {
            // the "+2" and "c-=1" are for the mapping between 
            // real field coords and the field with padded 2 cols
            r = (i / (m_cols + 2)) + 1;
            c = (i % (m_cols + 2)) + 1;
            c -= 1; 
        }

        /// <summary>
        /// return value is 0-based, but r and c are 1-based
        /// </summary>
        private int RowColToSignalIndex(int r, int c)
        {
            c++; // now c referes to the 1-based col index in the padded field
            return ((r - 1) * (m_cols + 2)) + (c - 1);
        }
        
        private int InterpretHighestSignal(State s, int maxr, int maxc)
        {
            var myPos = s.Me.Position;
            var rPos = new Position(maxr, maxc);
            int rDiff = maxr - myPos.Row;
            int cDiff = maxc - myPos.Col;

            foreach (var player in s.OurPlayersList)
            {
                if (player.Position.Equals(rPos))
                    return SoccerAction.GetIndexFromAction(new SoccerAction(ActionTypes.Pass, player.Unum), 
                        Params.MoveKings, this.MyUnum);
            }

            if (rDiff == 0 && cDiff == 0)
            {
                return SoccerAction.GetIndexFromAction(new SoccerAction(ActionTypes.Hold), Params.MoveKings, this.MyUnum);
            }
            else if (rDiff == 0 || (Math.Abs(rDiff) <= Math.Abs(cDiff)))
            {
                return SoccerAction.GetIndexFromAction(new SoccerAction(
                    cDiff > 0 ? ActionTypes.MoveEast : ActionTypes.MoveWest
                    ), Params.MoveKings, this.MyUnum);
            }
            else if (cDiff == 0 || (Math.Abs(rDiff) > Math.Abs(cDiff)))
            {
                return SoccerAction.GetIndexFromAction(new SoccerAction(
                    rDiff > 0 ? ActionTypes.MoveSouth : ActionTypes.MoveNorth
                    ), Params.MoveKings, this.MyUnum);
            }

            return 0;
        }
    }
}
