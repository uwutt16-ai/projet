using ScheduleOne.DevUtilities;
using UnityEngine;

namespace ScheduleOne.PlayerScripts;

public class PlayerTeleporter : MonoBehaviour
{
	public void Teleport(Transform destination)
	{
		PlayerSingleton<PlayerMovement>.Instance.Teleport(destination.position);
		Player.Local.transform.rotation = destination.rotation;
		Player.Local.transform.eulerAngles = new Vector3(0f, Player.Local.transform.eulerAngles.y, 0f);
	}
}
