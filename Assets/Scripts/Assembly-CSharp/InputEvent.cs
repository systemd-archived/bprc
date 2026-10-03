using UnityEngine;

public struct InputEvent
{
	public enum EventType
	{
		Press = 0,
		Release = 1,
		MouseEnter = 2,
		MouseLeave = 3,
		MouseReturn = 4,
		Drag = 5
	}

	public EventType type;

	public Vector3 position;

	public InputEvent(EventType type, Vector3 position)
	{
		this.type = type;
		this.position = position;
	}
}
