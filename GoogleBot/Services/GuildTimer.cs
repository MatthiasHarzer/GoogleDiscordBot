using System;
using System.Collections.Generic;
using System.Timers;

namespace GoogleBot.Services;

/// <summary>
/// A global timer with timer ids and cross command use
/// </summary>
public class GuildTimer
{
    public class Timed
    {
        private long time = 100;
        public Action Callback;
        private Timer? timer;
        private bool repeat = false;
        public bool Running => timer?.Enabled ?? false;
        
        public string Id { get; }

        public Timed(Action cb, string id)
        {
            Id = id;
            Callback = cb;
 
        }
        
        /// <summary>
        /// Defines the time of the timer. Milliseconds, seconds and minutes will add up.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds</param>
        /// <param name="seconds">The number of seconds</param>
        /// <param name="minutes">The number of minutes</param>
        /// <returns>The Timed instance</returns>
        public Timed In(long milliseconds = 0, int seconds = 0, int minutes = 0)
        {
            time = milliseconds + seconds * 1000 + minutes * 60 * 1000;
            repeat = false;
            return this;
        }

        /// <summary>
        /// See <see cref="Timed.In">Timed.In</see>, but the timer repeats
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds</param>
        /// <param name="seconds">The number of seconds</param>
        /// <param name="minutes">The number of minutes</param>
        /// <returns>The Timed instance</returns>
        public Timed Every(long milliseconds = 0, int seconds = 0, int minutes = 0)
        {
            time = milliseconds + seconds * 1000 + minutes * 60 * 1000;
            repeat = true;
            return this;
        }

        /// <summary>
        /// Stops the running timer
        /// </summary>
        public void Stop()
        {
            timer?.Stop();
        }
        
        /// <summary>
        /// Starts the timer
        /// </summary>
        /// <returns></returns>
        public Timed Start()
        {
            timer = new Timer(time);
            timer.AutoReset = false;
            timer.Elapsed += (_, _) => Callback();
            timer.Enabled = true;
            timer.AutoReset = repeat;
            return this;
        }
    }
    

    private readonly List<Timed> timedList = new List<Timed>();

    /// <summary>
    /// Sets a new callback of a timer instance. Uses an existing instance when the ids matches (stops old timer)
    /// </summary>
    /// <param name="cb">The callback when the timer resolves</param>
    /// <param name="id">The timers id. If null a random, unique id will be generated</param>
    /// <returns>A timed instance</returns>
    public Timed Run(Action cb, string? id = null)
    {
        id ??= Util.RandomUniqueString(timedList.ConvertAll(t => t.Id));
        
        Timed? existing = Get(id);
        if (existing == null)
        {
            Timed timer = new Timed(cb, id);
            timedList.Add(timer);
            return timer;
        }
        existing.Stop();
        existing.Callback = cb;
        
        return existing;
    }
    

    /// <summary>
    /// Stops all timers with the given id
    /// </summary>
    /// <param name="id">The timers id</param>
    public void Stop(string id)
    {
        timedList.FindAll(f => f.Id == id).ForEach(t=>t.Stop());
    }

    /// <summary>
    /// Gets a timer with a given id. 
    /// </summary>
    /// <param name="id">The timers id</param>
    /// <returns>The instance of the timer or null if none if a matching id exists</returns>
    private Timed? Get(string id)
    {
        return timedList.Find(t => t.Id == id);
    }

    
}