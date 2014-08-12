using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Scheduler.Heuristics
{
    public interface ISchedulerHeuristics
    {
        TaskScheduler.ActiveTask Choose(List<IEnumerable<TaskScheduler.ActiveTask>> estimations, ILookup<string, TaskScheduler.IResourceState> states);
    }

    public class HeuristicsNameAttribute : Attribute
    {
        public string Name
        {
            get;
            private set;
        }

        public HeuristicsNameAttribute(string name)
        {
            Name = name;
        }
    }

    [HeuristicsName("Stub")]
    [System.ComponentModel.Composition.Export(typeof(ISchedulerHeuristics))]
    public class StubHeuristics : ISchedulerHeuristics
    {
        private long taskPosition;

        public TaskScheduler.ActiveTask Choose(List<IEnumerable<TaskScheduler.ActiveTask>> estimations, ILookup<string, TaskScheduler.IResourceState> states)
        {
            var seq = estimations[0];
            estimations.Remove(seq);
            int idx;
            idx = (int)taskPosition % seq.Count();
            taskPosition++;
            return seq.ElementAt(idx);        
        }
    }

    [HeuristicsName("MinMin")]
    [System.ComponentModel.Composition.Export(typeof(ISchedulerHeuristics))]
    public class MinMinHeuristics : ISchedulerHeuristics
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public TaskScheduler.ActiveTask Choose(List<IEnumerable<TaskScheduler.ActiveTask>> estimations, ILookup<string, TaskScheduler.IResourceState> states)
        {
            
            /*var firsts = estimations.Select(e => e.First());
            double minTime = firsts.Select(e => e.Estimation.Time + states[e.ResourceName].EstimatedAvailabilityTime).Min();*/
            //var bestTaskEstimationList = estimations.First(est => est.First().Estimation.Time == minTime);
            //var bestTaskEstimationList = estimations.First(est => est.First().Estimation.Time + states[est.First().ResourceName].EstimatedAvailabilityTime == minTime);
            //var bestTaskOnResource = bestTaskEstimationList.First();

            var seq = estimations.OrderBy(/*e => e.First().Estimation.Time + (states[e.First().Destination.ResourceName].Join(
                e.First().Destination.NodeNames,
                s => s.NodeName,
                n => n,
                (s, n) => s)).Max(es => es.EstimatedAvailabilityTime)*/
                e => e.First().Destination.NodeNames.GroupBy(n => n).Max(g => g.Count()) * e.First().Estimation.Time + e.First().EstimatedLaunchTime);
            var sequence = seq.First();
            estimations.Remove(sequence);
            return sequence.First();
            //return firsts.Where(est => est.Estimation.Time + states[est.ResourceName].EstimatedAvailabilityTime == minTime).ElementAt(0);
        }
    }

    [HeuristicsName("MaxMin")]
    [System.ComponentModel.Composition.Export(typeof(ISchedulerHeuristics))]
    public class MaxMinHeuristics : ISchedulerHeuristics
    {
        public TaskScheduler.ActiveTask Choose(List<IEnumerable<TaskScheduler.ActiveTask>> estimations, ILookup<string, TaskScheduler.IResourceState> states)
        {
            //var firsts = estimations.Select(e => e.First());
            //double maxTime = estimations.Max(est => est.First().Estimation.Time); // среди лучших ресурсов
            //double maxTime = firsts.Select(e => e.Estimation.Time + states[e.ResourceName].EstimatedAvailabilityTime).Max();
            /*var bestTaskEstimationList = estimations.First(est => est.First().Estimation.Time == maxTime);
            var bestTaskOnResource = bestTaskEstimationList.First();*/
            //return firsts.Where(est => est.Estimation.Time + states[est.ResourceName].EstimatedAvailabilityTime == maxTime).ElementAt(0);
            var seq = estimations.OrderByDescending(e => e.First().Destination.NodeNames.GroupBy(n => n).Max(g => g.Count()) * e.First().Estimation.Time + e.First().EstimatedLaunchTime
                /*(states[e.First().Destination.ResourceName].Join(
                e.First().Destination.NodeNames,
                s => s.NodeName,
                n => n,
                (s, n) => s)).Max(es => es.EstimatedAvailabilityTime)*/).First();
            estimations.Remove(seq);
            return seq.First();
        }
    }

    [HeuristicsName("Sufferage")]
    [System.ComponentModel.Composition.Export(typeof(ISchedulerHeuristics))]
    public class SufferageHeuristics : ISchedulerHeuristics
    {
        public TaskScheduler.ActiveTask Choose(List<IEnumerable<TaskScheduler.ActiveTask>> estimations, ILookup<string, TaskScheduler.IResourceState> states)
        {
            /*var pairs = estimations.Select(e => new TaskScheduler.ActiveTask[] {e.First(), e.ElementAt(1)});
            double maxDiffTime = pairs.Select(p => p[1].Estimation.Time - p[0].Estimation.Time + states[p[1].ResourceName].EstimatedAvailabilityTime - states[p[0].ResourceName].EstimatedAvailabilityTime).Max();*/
            //return pairs.Where(p => p[1].Estimation.Time - p[0].Estimation.Time + states[p[1].ResourceName].EstimatedAvailabilityTime - states[p[0].ResourceName].EstimatedAvailabilityTime == maxDiffTime).First()[0];
            var seq = estimations.OrderByDescending(e =>
                /*e.ElementAt(1).Estimation.Time -
                e.First().Estimation.Time +
                states[e.ElementAt(1).Destination.ResourceName].Join(e.ElementAt(1).Destination.NodeNames, s => s.NodeName, n => n, (s, n) => s).Max(es => es.EstimatedAvailabilityTime) -
                states[e.First().Destination.ResourceName].Join(e.First().Destination.NodeNames, s => s.NodeName, n => n, (s, n) => s).Max(es => es.EstimatedAvailabilityTime)*/
                e.ElementAt(1).Destination.NodeNames.GroupBy(n => n).Max(g => g.Count()) * e.ElementAt(1).Estimation.Time -
                e.First().Destination.NodeNames.GroupBy(n => n).Max(g => g.Count()) * e.First().Estimation.Time +
                e.ElementAt(1).EstimatedLaunchTime -
                e.First().EstimatedLaunchTime                
                )
                .First();
            estimations.Remove(seq);
            return seq.First();
        }
    }
}
