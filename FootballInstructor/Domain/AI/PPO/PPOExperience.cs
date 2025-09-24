using System;
using System.Collections.Generic;
using FootballInstructor.Domain.Model;

namespace FootballInstructor.Domain.AI.PPO
{
    /// <summary>
    /// Single step experience for PPO
    /// </summary>
    public class PPOExperience
    {
        public float[] State { get; set; } = Array.Empty<float>();
        public float[] Action { get; set; } = Array.Empty<float>();
        public float Reward { get; set; }
        public float[] NextState { get; set; } = Array.Empty<float>();
        public bool IsTerminal { get; set; }
        public float Value { get; set; }
        public float LogProb { get; set; }
        public float Advantage { get; set; }
        public float Return { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Complete trajectory (episode) for PPO training
    /// </summary>
    public class PPOTrajectory
    {
        public List<PPOExperience> Experiences { get; set; } = new();
        public float TotalReturn { get; set; }
        public int Length => Experiences.Count;
        public bool IsComplete { get; set; }

        public void AddExperience(PPOExperience experience)
        {
            Experiences.Add(experience);
            TotalReturn += experience.Reward;
        }

        /// <summary>
        /// Compute GAE advantages for this trajectory
        /// </summary>
        public void ComputeAdvantages(float gamma = 0.99f, float lambda = 0.95f)
        {
            if (Experiences.Count == 0) return;

            float gae = 0f;
            
            // Work backwards through trajectory for GAE computation
            for (int t = Experiences.Count - 1; t >= 0; t--)
            {
                var exp = Experiences[t];
                
                // Get next state value (0 if terminal)
                float nextValue = 0f;
                if (!exp.IsTerminal && t < Experiences.Count - 1)
                {
                    nextValue = Experiences[t + 1].Value;
                }
                
                // Temporal difference error
                float delta = exp.Reward + gamma * nextValue - exp.Value;
                
                // GAE accumulation
                gae = delta + gamma * lambda * gae;
                exp.Advantage = gae;
                
                // Return (for value function training)
                exp.Return = exp.Advantage + exp.Value;
            }
        }

        /// <summary>
        /// Normalize advantages to improve training stability
        /// </summary>
        public void NormalizeAdvantages()
        {
            if (Experiences.Count <= 1) return;

            // Calculate mean and std of advantages
            float mean = 0f;
            foreach (var exp in Experiences)
                mean += exp.Advantage;
            mean /= Experiences.Count;

            float variance = 0f;
            foreach (var exp in Experiences)
            {
                float diff = exp.Advantage - mean;
                variance += diff * diff;
            }
            variance /= Experiences.Count;
            float std = (float)Math.Sqrt(variance + 1e-8); // Add small epsilon for numerical stability

            // Normalize
            foreach (var exp in Experiences)
            {
                exp.Advantage = (exp.Advantage - mean) / std;
            }
        }
    }

    /// <summary>
    /// Buffer for collecting multiple trajectories before training
    /// </summary>
    public class PPOBuffer
    {
        private readonly List<PPOTrajectory> _trajectories = new();
        private readonly int _maxTrajectories;

        public PPOBuffer(int maxTrajectories = 10)
        {
            _maxTrajectories = maxTrajectories;
        }

        public void AddTrajectory(PPOTrajectory trajectory)
        {
            trajectory.ComputeAdvantages();
            trajectory.NormalizeAdvantages();
            
            _trajectories.Add(trajectory);
            
            // Keep buffer size manageable
            while (_trajectories.Count > _maxTrajectories)
            {
                _trajectories.RemoveAt(0);
            }
        }

        public List<PPOExperience> GetAllExperiences()
        {
            var allExperiences = new List<PPOExperience>();
            foreach (var trajectory in _trajectories)
            {
                allExperiences.AddRange(trajectory.Experiences);
            }
            return allExperiences;
        }

        public void Clear()
        {
            _trajectories.Clear();
        }

        public int TrajectoryCount => _trajectories.Count;
        public int TotalExperienceCount
        {
            get
            {
                int count = 0;
                foreach (var traj in _trajectories)
                    count += traj.Length;
                return count;
            }
        }
    }
}