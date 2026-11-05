# Temporio

Temporio is a timer scheduling library for Unity, with one-time and repeating timers, automatic cancellation guards, and configurable time and update providers. It minimizes allocations by reusing internal timer, builder, action, and guard objects through pools. Parameterized callbacks help avoid additional allocations from lambda closures.

## Table of Contest

- [Installation](#installation)
- [Core concepts](#core-concepts)
  - [TimerManager](#timermanager)
  - [Timer](#timer)
  - [TimerBuilder](#timerbuilder)
- [Basic usage](#basic-usage)
  - [Creating timer manager](#creating-timer-manager)
  - [Scheduling timer](#scheduling-timer)
    - [One-time timer](#one-time-timer)
    - [Repeating timer](#repeating-timer)
    - [Cancelling timer](#cancelling-timer)
    - [PlayerLoopTiming](#playerlooptiming)
  - [Error handling](#error-handling)
- [Advanced](#advanced)
  - [Actions with context](#actions-with-context)
  - [Actions with parameter](#actions-with-parameter)
  - [Guards](#guards)
  - [MissingPeriodBehaviour](#missingperiodbehaviour)
  - [ITimeProvider](#itimeprovider)
  - [IUpdateProvider](#iupdateprovider)

## Installation

Temporio is currently under development and is available through Unity Package Manager (UPM) at:

```text
https://github.com/damdordev/Temporio.git#1.0.0-preview
```

Requires Unity 2022.3 or later.

**Via Package Manager**

1. Open **Window > Package Manager** in Unity.
2. Click **+** and select **Add package from git URL...**
3. Paste the URL above and click **Add**.

**Via manifest.json**

Add the package to the `dependencies` object in your project's `Packages/manifest.json`, preserving any existing entries:

```json
{
  "dependencies": {
    "com.damdor.temporio": "https://github.com/damdordev/Temporio.git#1.0.0-preview"
  }
}
```

## Core concepts

### TimerManager

`TimerManager` owns scheduled timers and their internal pools. Its time provider determines the current time, exposed as `Now`, and its update provider determines when timers are processed. Call `Run()` once to start receiving updates, and `Dispose()` when the manager is no longer needed.

### Timer

`Timer` is a lightweight, readonly struct returned when a timer is scheduled. Use it to cancel the timer or inspect properties such as `IsAlive`, `IsRepeating`, `InvocationCount`, `NextInvocationTime`, and `TimeToNextInvocation`. The handle does not keep its manager alive. `IsAlive` becomes `false` after completion or cancellation.

### TimerBuilder

`TimerManager.Schedule()` returns a `TimerBuilder` for configuring the start time, repeat interval, actions, guards, and player loop phase. Calling the builder's `Run()` schedules the timer and returns its `Timer` handle.

Builders are short-lived: configure and run them in the same frame. A builder becomes invalid after `Run()`, when its manager is disposed, or when the manager clears unused builders during its next `Update`. Keep the returned `Timer` handle if you need to manage the timer later.

## Basic usage

The snippets below use `System`, `Damdor.Temporio`, and `UnityEngine`. They assume a running `timerManager` unless they create one explicitly.

### Creating timer manager

Create a manager with `UnityTimeProvider` and start it with `Run()`. `MonoUpdateProvider` forwards Unity's `Update`, `FixedUpdate`, and `LateUpdate` callbacks to the manager.

```csharp
using System;
using Damdor.Temporio;
using UnityEngine;

public class TimerExample : MonoBehaviour
{
    private TimerManager timerManager;

    private void Start()
    {
        timerManager = new TimerManager(new UnityTimeProvider());
        timerManager.Run();

        timerManager.Schedule()
            .After(TimeSpan.FromSeconds(2))
            .Action(() => Debug.Log("Two seconds have passed"))
            .Run();
    }

    private void OnDestroy()
    {
        timerManager?.Dispose();
    }
}
```

### Scheduling timer

#### One-time timer

Use `After` for a delay relative to the manager's current time:

```csharp
timerManager.Schedule()
    .After(TimeSpan.FromSeconds(5))
    .Action(() => Debug.Log("Five seconds have passed"))
    .Run();
```

Use `When` for a specific timestamp in the time provider's clock:

```csharp
DateTime deadline = timerManager.Now.AddSeconds(5);

timerManager.Schedule()
    .When(deadline)
    .Action(() => Debug.Log("Deadline reached"))
    .Run();
```

Choose either `When` or `After`; the initial time can only be configured once. Without either method, the timer is due immediately. Due timers, including those scheduled in the past, run during the next corresponding update phase rather than inside the builder's `Run()` call.

#### Repeating timer

Use `Every` to set the repeat interval, and optionally `InvocationLimit` to stop after a fixed number of invocations:

```csharp
timerManager.Schedule()
    .After(TimeSpan.FromSeconds(2))
    .Every(TimeSpan.FromSeconds(1))
    .InvocationLimit(5)
    .Action(() => Debug.Log("Tick"))
    .Run();
```

This timer first runs after two seconds, then every second, for five invocations total. The limit includes the first invocation. Omitting `InvocationLimit`, or setting it to `0`, allows unlimited repetitions. The repeat interval must be greater than zero.

#### Cancelling timer

Store the handle returned by `Run()` and call `Cancel()`:

```csharp
var timer = timerManager.Schedule()
    .After(TimeSpan.FromSeconds(10))
    .Action(() => Debug.Log("Timer finished"))
    .Run();

bool cancelled = timer.Cancel();
Debug.Log($"Cancelled: {cancelled}, alive: {timer.IsAlive}");
```

`Cancel()` returns `true` when it cancels an active timer, or `false` if the timer is already inactive. If called from an action, the remaining actions in that invocation still execute.

#### PlayerLoopTiming

Use `Timing` when building a timer to choose its Unity player loop phase. The default is `Update`.

```csharp
timerManager.Schedule()
    .Every(TimeSpan.FromSeconds(1))
    .Timing(PlayerLoopTiming.Update)
    .Action(() => Debug.Log("Update timer"))
    .Run();

timerManager.Schedule()
    .Every(TimeSpan.FromSeconds(1))
    .Timing(PlayerLoopTiming.FixedUpdate)
    .Action(() => Debug.Log("FixedUpdate timer"))
    .Run();

timerManager.Schedule()
    .Every(TimeSpan.FromSeconds(1))
    .Timing(PlayerLoopTiming.LateUpdate)
    .Action(() => Debug.Log("LateUpdate timer"))
    .Run();
```

### Error handling

`TimerManager` catches exceptions thrown by timer actions and guards. Configure logging, a custom handler, and whether an action exception should cancel a repeating timer:

```csharp
timerManager.LogErrors = true;
timerManager.CancelTimerOnException = true;
timerManager.SetErrorHandler((debugName, exception) =>
{
    Debug.LogError($"Timer '{debugName}' failed: {exception.Message}");
});

timerManager.Schedule()
    .After(TimeSpan.FromSeconds(1))
    .Every(TimeSpan.FromSeconds(1))
    .DebugName("Refresh inventory")
    .Action(() => throw new InvalidOperationException("Refresh failed"))
    .Run();
```

`LogErrors` defaults to `true` and logs exceptions through Unity's `Debug.LogException`. Set it to `false` to disable built-in logging; the custom handler still runs. Pass `null` to `SetErrorHandler` to remove the handler.

`CancelTimerOnException` defaults to `false`. When enabled, an action exception cancels future repetitions after the current invocation's remaining actions execute. A guard exception always cancels its timer. Exceptions thrown by the custom handler are also caught and logged when `LogErrors` is enabled.

## Advanced

### Actions with context

An action can receive a `TimerInvocationContext` with information about its invocation:

```csharp
timerManager.Schedule()
    .After(TimeSpan.FromSeconds(1))
    .Every(TimeSpan.FromSeconds(1))
    .Action((TimerInvocationContext context) =>
    {
        Debug.Log($"Invocation {context.InvocationNumber}: " +
                  $"scheduled for {context.ScheduledTime}, " +
                  $"processed at {context.InvocationTime}, " +
                  $"delay {context.InvocationDelay.TotalMilliseconds} ms");
    })
    .Run();
```

`InvocationNumber` starts at `0`. `ScheduledTime` is the intended execution time, `InvocationTime` is the manager's time when processing the update, and `InvocationDelay` is the difference between them. You can chain multiple `Action` calls; they execute in registration order.

### Actions with parameter

Pass data directly to an action to avoid capturing local variables in a lambda. This is especially useful when scheduling many timers, because capturing lambdas can allocate closure objects in addition to delegates.

Given an application model:

```csharp
public class EventModel
{
    public string Id { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsCancelled { get; set; }
}
```

Compare capturing `evt` with passing it as a parameter. These are alternative ways to schedule the same event:

```csharp
EventModel evt = new EventModel
{
    Id = "summer-event",
    EndTime = timerManager.Now.AddMinutes(10)
};

// Captures the local variable evt in a closure.
timerManager.Schedule()
    .When(evt.EndTime)
    .Action(() => Debug.Log($"Event {evt.Id} is finished"))
    .Run();

// Passes evt directly; the static lambda cannot capture local variables.
timerManager.Schedule()
    .When(evt.EndTime)
    .Action(evt, static e => Debug.Log($"Event {e.Id} is finished"))
    .Run();
```

The parameter overload avoids the closure allocation and lets Temporio reuse its internal parameterized action objects. Code inside the callback, such as string interpolation and logging, can still allocate.

You can also receive both the context and the parameter, in that order:

```csharp
timerManager.Schedule()
    .When(evt.EndTime)
    .Action(evt, static (context, e) =>
        Debug.Log($"Event {e.Id} finished {context.InvocationDelay} ago"))
    .Run();
```

### Guards

Guards automatically cancel a timer when its owner is destroyed or a cancellation condition becomes true. They are checked during the timer's selected update phase, even before its next invocation is due. Multiple guards can be combined; any one can cancel the timer.

**GameObject lifetime** — cancel when a GameObject is destroyed. In a `MonoBehaviour`:

```csharp
timerManager.Schedule()
    .Every(TimeSpan.FromSeconds(1))
    .CancelOnDestroy(gameObject)
    .Action(() => Debug.Log("Owner is still alive"))
    .Run();
```

**Condition** — cancel when the predicate returns `true`:

```csharp
bool shouldCancel = false;

timerManager.Schedule()
    .Every(TimeSpan.FromSeconds(1))
    .CancelIf(() => shouldCancel)
    .Action(() => Debug.Log("Still running"))
    .Run();

// The timer will be cancelled during its next update phase.
shouldCancel = true;
```

**Condition with a parameter** — pass state without capturing it in a closure, using the `EventModel` above:

```csharp
timerManager.Schedule()
    .When(evt.EndTime)
    .CancelIf(evt, static e => e.IsCancelled)
    .Action(evt, static e => Debug.Log($"Event {e.Id} is finished"))
    .Run();
```

### MissingPeriodBehaviour

A repeating timer can miss periods when a frame takes too long, the application stops receiving updates, or its time provider jumps forward. `MissingPeriodBehaviour` controls what happens when processing resumes:

| Option | Behaviour |
| --- | --- |
| `RunOnce` (default) | Invoke once, skip the other overdue periods, and schedule the next invocation on the original interval grid. |
| `RunAll` | Invoke once for every overdue scheduled time during the same update, subject to cancellation and the invocation limit. |

For example, if a timer is due at seconds 1, 2, and 3, but the next update arrives at second 3.5, `RunOnce` invokes once and schedules second 4. `RunAll` invokes three times, for seconds 1, 2, and 3, then schedules second 4.

```csharp
timerManager.Schedule()
    .After(TimeSpan.FromSeconds(1))
    .Every(TimeSpan.FromSeconds(1))
    .MissingPeriodBehaviour(MissingPeriodBehaviour.RunAll)
    .Action(context => Debug.Log($"Processing period {context.ScheduledTime}"))
    .Run();
```

Choose `RunOnce` when only the latest update matters. Choose `RunAll` when every elapsed period must be processed, allowing for potentially many callbacks in one update after a long delay.

### ITimeProvider

`ITimeProvider` supplies the manager's current time through its `DateTime Now` property. It determines when timers become due; the update provider determines when they are checked.

| Provider | Typical use |
| --- | --- |
| `UnityTimeProvider` | Normal gameplay. Uses scaled Unity time by default, so timers follow `Time.timeScale`. Prefer `After` for gameplay delays. Pass `useScaledTime: false` to follow unscaled real time instead. |
| `LocalTimeProvider` | Metagame schedules, such as event end times. Uses `DateTime.Now`; schedule local-clock timestamps with `When`. System clock changes affect these timers. |
| `ManualTimeProvider` | Tests and simulations. Set `Now` explicitly to advance time without waiting. |

For local-clock event deadlines, replace `new UnityTimeProvider()` with `new LocalTimeProvider()` in the manager setup above, then schedule an absolute timestamp:

```csharp
timerManager.Schedule()
    .When(timerManager.Now.AddHours(1))
    .Action(() => Debug.Log("Event finished"))
    .Run();
```

Supply timestamps from the same clock as the provider. `UnityTimeProvider` anchors a date at construction and advances it using Unity's elapsed time, so scaled gameplay time can diverge from the local system clock.

Implement `ITimeProvider` or extend an existing provider to customize time. For example:

```csharp
public class ShiftedTimeProvider : UnityTimeProvider
{
    public TimeSpan Shift { get; set; }

    public override DateTime Now => base.Now + Shift;
}
```

Pass an instance to `TimerManager` as its first constructor argument. Changing `Shift` changes the time seen by that manager and can make timers immediately overdue.

### IUpdateProvider

`IUpdateProvider` registers and unregisters callbacks for each `PlayerLoopTiming`. It drives timer processing independently of the clock. In most cases, use the default `MonoUpdateProvider`, which forwards Unity's player loop callbacks automatically.

Use `ManualUpdateProvider` when you need more control, such as in tests or a custom loop. Advancing a `ManualTimeProvider` changes the clock; calling `Tick` processes timers for the selected phase:

```csharp
var time = new ManualTimeProvider(new DateTime(2026, 1, 1));
using var updates = new ManualUpdateProvider();
using var manager = new TimerManager(time, updates);
manager.Run();

manager.Schedule()
    .After(TimeSpan.FromSeconds(5))
    .Action(() => Debug.Log("Five simulated seconds have passed"))
    .Run();

time.Now = time.Now.AddSeconds(5);
updates.Tick(PlayerLoopTiming.Update);
```

You can implement `IUpdateProvider` yourself, or derive from `BaseUpdateProvider` to reuse registration and dispatch logic:

```csharp
public class CustomUpdateProvider : BaseUpdateProvider
{
    // Call these from the corresponding phases of your own loop.
    public void OnUpdate() => Run(PlayerLoopTiming.Update);
    public void OnFixedUpdate() => Run(PlayerLoopTiming.FixedUpdate);
    public void OnLateUpdate() => Run(PlayerLoopTiming.LateUpdate);
}
```

Pass your provider as the second `TimerManager` constructor argument and drive it from your loop. If the provider subscribes to external events or owns resources, release them in its `Dispose()` implementation and call `base.Dispose()` to clear registered receivers. Dispose the manager before disposing its update provider.
