
using System;


namespace CR2W.Types.W3
{
	public class CMenuPauseParam : CObject, IMenuTimeParam
	{
		[REDProp("pauseType")]
		public EMenuPauseType PauseType { get; set; }

	}
}
