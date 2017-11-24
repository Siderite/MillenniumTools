using System;

namespace BS.Utilities
{
	public enum PingResponseType
	{
		Ok = 0,
		CouldNotResolveHost,
		RequestTimedOut,
		ConnectionError,
		InternalError,
		Canceled
	}
}
