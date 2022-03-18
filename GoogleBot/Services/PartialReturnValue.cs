using System;
using System.Timers;

namespace GoogleBot.Services;

public class PartialReturnValue<T>
{
    /// <summary>
    /// The current return value
    /// </summary>
    public T? Value { get; private set; }

    private T? targetValue;
    private bool updateEnabled = true;

    public delegate void CallBackFunction(T? value);
    
    /// <summary>
    /// Whether the return data was finished transmitting or not
    /// </summary>
    public bool Finished { get; private set; } = false;

    private CallBackFunction? onChange;
    private CallBackFunction? onFinish;
    private Action? onTimeout;
    
    /// <summary>
    /// Called when <see cref="Value"/> changes
    /// </summary>
    public void OnChange(CallBackFunction f)
    {
        onChange = f;
    }

    /// <summary>
    /// Called when there is no more data to expect
    /// </summary>
    public void OnFinish(CallBackFunction f)
    {
        onFinish = f;
    }

    /// <summary>
    /// Called when <see cref="Value"/> didn't change for <see cref="Timeout"/>
    /// </summary>
    public void OnTimeout(Action f)
    {
        onTimeout = f;
    }

    /// <summary>
    /// The time in ms when to raise <see cref="OnTimeout"/> when the value didn't change
    /// Default: 1 minute (60 * 1000)
    /// </summary>
    public float Timeout { get; set; } = 1 * 60 * 1000;

    /// <summary>
    /// The minimum time to wait before updating the <see cref="Value"/> to prevent many updates in too short time
    /// Default: 2 secs
    /// </summary>
    public float MinWaitTime { get; set; } = 2 * 1000; 

    private Timer? timer;
    private Timer? minTimer;
    

    /// <summary>
    /// Set the current value and update callbacks
    /// </summary>
    /// <param name="value">The new value</param>
    public void Set(T value)
    {
        if(Finished) return;
        
        timer?.Stop();
        timer = new Timer(Timeout);
        timer.Elapsed += OnTimedEvent;
        timer.Enabled = true;

        if (!updateEnabled)
        {
            targetValue = value;
            SetMinTimer();
            return;
        }
        
        Value = value;
        onChange?.Invoke(Value);
    }

    private void SetMinTimer()
    {
        minTimer?.Stop();
        minTimer = new Timer(MinWaitTime);
        minTimer.Elapsed += MinTimerElapsed;
        minTimer.Enabled = true;
    }

    private void MinTimerElapsed(object? source, ElapsedEventArgs e)
    {
        if (targetValue != null && !targetValue.Equals(Value))
        {
            targetValue = Value;
            updateEnabled = false;
            SetMinTimer();
        }
        else
        {
            updateEnabled = true;
            minTimer?.Stop();
        }

    }

    /// <summary>
    /// Call to complete the partial data
    /// </summary>
    public void Complete(T value)
    {
        timer?.Stop();
        Value = value;
        Finished = true;
        onFinish?.Invoke(Value);
        onChange = null;
        onTimeout = null;
        onFinish = null;
    }

    private void OnTimedEvent(object? source, ElapsedEventArgs e)
    {
        onTimeout?.Invoke();
    }
}