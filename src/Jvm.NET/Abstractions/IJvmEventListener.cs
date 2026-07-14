namespace Jvm.NET.Abstractions;

/// <summary>
/// Subscribes managed callbacks to JVMTI events. Requires JVMTI to be enabled at
/// startup (<see cref="JvmInitializationOptions.EnableEventListening"/>).
///
/// Each <c>Subscribe*</c> method returns an <see cref="IDisposable"/> that, when
/// disposed, unregisters the callback AND turns off the corresponding JVMTI event
/// if no other subscriber remains.
/// </summary>
public interface IJvmEventListener
{
    IDisposable SubscribeMethodEntry(Action<MethodEntryEventData> handler);
    IDisposable SubscribeMethodExit(Action<MethodExitEventData> handler);
    IDisposable SubscribeClassLoad(Action<ClassLoadEventData> handler);
    IDisposable SubscribeClassPrepare(Action<ClassPrepareEventData> handler);
    IDisposable SubscribeThreadStart(Action<ThreadStartEventData> handler);
    IDisposable SubscribeThreadEnd(Action<ThreadEndEventData> handler);
    IDisposable SubscribeVmInit(Action<VmInitEventData> handler);
    IDisposable SubscribeVmDeath(Action<VmDeathEventData> handler);
    IDisposable SubscribeException(Action<ExceptionEventData> handler);
}
