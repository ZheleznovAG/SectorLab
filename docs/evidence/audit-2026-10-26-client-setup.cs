SectorLab.Network.LoopbackChannel.CreatePair(out var hostEnd, out var clientEnd);
System.AppDomain.CurrentDomain.SetData("SectorLabAuditHost", hostEnd);
SectorLab.Network.NetworkLaunch.Client(clientEnd);
UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
return "Client Main requested";
