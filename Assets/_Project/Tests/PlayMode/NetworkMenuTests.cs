using System.Collections;
using NUnit.Framework;
using SectorLab.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SectorLab.Tests
{
    public class NetworkMenuTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            NetworkLauncher.Leave();
            Time.timeScale = 1;
            Scene main = SceneManager.GetSceneByName("Main");
            if (main.IsValid() && main.isLoaded)
            {
                Scene empty = SceneManager.CreateScene("AfterMenu");
                SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }

        private static IEnumerator LoadMain()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return MainLifecycleTests.WaitForReady();
        }

        // Restart reloads Main by build index; wait for the new instance.
        private static IEnumerator WaitForReload(NetworkMenu old)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (old != null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return MainLifecycleTests.WaitForReady();
        }

        [UnityTest]
        public IEnumerator HeaderButtonOpensMenuBesidePauseAndRejectsBadAddresses()
        {
            yield return LoadMain();
            var menu = Object.FindAnyObjectByType<NetworkMenu>();
            var top = menu.transform.Find("TopBar");
            var pause = (RectTransform)top.Find("Pause");
            var network = (RectTransform)top.Find("Network");
            Assert.That(network, Is.Not.Null);
            Assert.That(pause.anchorMax.x, Is.LessThanOrEqualTo(network.anchorMin.x));
            Assert.That(menu.IsOpen, Is.False);
            var battle = Object.FindAnyObjectByType<SectorLab.Battle.BattleController>();
            network.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(battle.State.Clock.IsPaused, Is.True, "An open menu stops the local match.");
            menu.Address.text = "not-an-ip";
            menu.JoinButton.onClick.Invoke();
            Assert.That(NetworkLaunch.Role, Is.EqualTo(SessionRole.Offline));
            Assert.That(menu.Status, Does.Contain("IPv4"));
            menu.SetOpen(false);
            Assert.That(battle.State.Clock.IsPaused, Is.False);
        }

        [UnityTest]
        public IEnumerator FailedRoleSwitchRebuildsAnOfflineMatchAndPreservesItsErrorNotice()
        {
            LoopbackChannel.CreatePair(out _, out var client);
            NetworkLaunch.Client(client);
            yield return LoadMain();
            var menu = Object.FindAnyObjectByType<NetworkMenu>();
            menu.SetOpen(true);
            // Exercise the same failure path as a busy port, without depending on local socket occupancy.
            var relaunch = typeof(NetworkMenu).GetMethod("Relaunch",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            relaunch.Invoke(menu, new object[] {
                (System.Action)(() => throw new System.InvalidOperationException("Port busy"))
            });
            yield return WaitForReload(menu);
            menu = Object.FindAnyObjectByType<NetworkMenu>();
            var battle = Object.FindAnyObjectByType<SectorLab.Battle.BattleController>();
            Assert.That(NetworkLaunch.Role, Is.EqualTo(SessionRole.Offline));
            Assert.That(NetworkLaunch.Channel, Is.Null);
            Assert.That(Object.FindAnyObjectByType<NetworkMatchBootstrap>().Role, Is.EqualTo(SessionRole.Offline));
            Assert.That(battle.IsAuthority && battle.enabled, Is.True);
            Assert.That(Object.FindAnyObjectByType<SectorLab.AI.EnemyTacticsController>().enabled, Is.True);
            Assert.That(menu.IsOpen, Is.True);
            Assert.That(menu.Status, Does.Contain("Port busy"));
            Assert.That(NetworkLaunch.TakeNotice(), Is.Null, "The notice is consumed once by the fresh menu.");
            menu.SetOpen(false);
            double elapsed = battle.State.Clock.ElapsedSeconds;
            float deadline = Time.realtimeSinceStartup + 3;
            while (battle.State.Clock.ElapsedSeconds <= elapsed && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(battle.State.Clock.ElapsedSeconds, Is.GreaterThan(elapsed));
            Assert.That(battle.Commands.Execute(SectorLab.Battle.UnitCommand.MoveToRelay(
                SectorLab.Units.Team.Player, 0, 0)), Is.EqualTo(SectorLab.Battle.CommandResult.Accepted));
        }

        [UnityTest]
        public IEnumerator HostAndOfflineButtonsRestartMainInThatRole()
        {
            yield return LoadMain();
            var menu = Object.FindAnyObjectByType<NetworkMenu>();
            menu.SetOpen(true);
            menu.HostButton.onClick.Invoke();
            Assert.That(NetworkLaunch.Role, Is.EqualTo(SessionRole.Host));
            yield return WaitForReload(menu);
            var bootstrap = Object.FindAnyObjectByType<NetworkMatchBootstrap>();
            Assert.That(bootstrap.Role, Is.EqualTo(SessionRole.Host));
            Assert.That(bootstrap.WaitingForPeer, Is.True);
            menu = Object.FindAnyObjectByType<NetworkMenu>();
            Assert.That(menu.transform.Find("TopBar/Network").GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("Сеть: хост"));

            menu.SetOpen(true);
            menu.OfflineButton.onClick.Invoke();
            Assert.That(NetworkLaunch.Role, Is.EqualTo(SessionRole.Offline));
            yield return WaitForReload(menu);
            Assert.That(Object.FindAnyObjectByType<NetworkMatchBootstrap>().Role, Is.EqualTo(SessionRole.Offline));
        }
    }
}
