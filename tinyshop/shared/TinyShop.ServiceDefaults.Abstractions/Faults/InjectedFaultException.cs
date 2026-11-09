namespace TinyShop.ServiceDefaults.Abstractions.Faults;

/// <summary>
/// A failure caused on purpose by a fault switch (TS-016), never by a real problem. It travels like
/// any unhandled exception: logged as an error with its stack trace, and answered with the service's
/// generic error (HTTP 500 internal_error, gRPC INTERNAL). Its own type and message make it
/// recognisable in the logs (decision 8).
/// </summary>
public sealed class InjectedFaultException(string message) : Exception(message);
