using System.Collections.Generic;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SkillsPlusPlus.Networking
{
    /// <summary>
    /// Host-authoritative networking for Skills++, built on R2API.Networking messages so it needs no UNet weaver.
    ///
    /// The host owns all skill point data. It sends <see cref="SyncSkillPointsMessage"/> to every client whenever a
    /// player's points or levels change, and clients ask the host to buy a level with <see cref="BuySkillMessage"/>.
    /// </summary>
    internal static class SkillsNetwork
    {
        internal static void Register()
        {
            NetworkingAPI.RegisterMessageType<SyncSkillPointsMessage>();
            NetworkingAPI.RegisterMessageType<BuySkillMessage>();
            NetworkingAPI.RegisterMessageType<RespecMessage>();
        }
    }

    /// <summary>Host to clients: the full skill point state of one player master.</summary>
    internal class SyncSkillPointsMessage : INetMessage
    {
        private NetworkInstanceId masterId;
        private bool survivorEnabled;
        private int earned;
        private int unspent;
        private int levelsPerSkillPoint;
        private bool multScalingLinear;
        private int maxSkillLevel;
        private List<KeyValuePair<string, int>> levels = new List<KeyValuePair<string, int>>();

        public SyncSkillPointsMessage() { }

        internal SyncSkillPointsMessage(SkillPointsController controller)
        {
            masterId = controller.MasterNetId;
            survivorEnabled = controller.isSurvivorEnabled;
            earned = controller.earnedSkillPoints;
            unspent = controller.unspentSkillPoints;
            levelsPerSkillPoint = controller.levelsPerSkillPoint;
            multScalingLinear = controller.multScalingLinear;
            maxSkillLevel = controller.maxSkillLevel;
            levels = controller.GetAllLevels();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(masterId);
            writer.Write(survivorEnabled);
            writer.WritePackedUInt32((uint)earned);
            writer.WritePackedUInt32((uint)unspent);
            writer.WritePackedUInt32((uint)levelsPerSkillPoint);
            writer.Write(multScalingLinear);
            writer.WritePackedUInt32((uint)maxSkillLevel);
            writer.WritePackedUInt32((uint)levels.Count);
            foreach (var pair in levels)
            {
                writer.Write(pair.Key);
                writer.WritePackedUInt32((uint)pair.Value);
            }
        }

        public void Deserialize(NetworkReader reader)
        {
            masterId = reader.ReadNetworkId();
            survivorEnabled = reader.ReadBoolean();
            earned = (int)reader.ReadPackedUInt32();
            unspent = (int)reader.ReadPackedUInt32();
            levelsPerSkillPoint = (int)reader.ReadPackedUInt32();
            multScalingLinear = reader.ReadBoolean();
            maxSkillLevel = (int)reader.ReadPackedUInt32();
            int count = (int)reader.ReadPackedUInt32();
            levels = new List<KeyValuePair<string, int>>(count);
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                int level = (int)reader.ReadPackedUInt32();
                levels.Add(new KeyValuePair<string, int>(name, level));
            }
        }

        public void OnReceived()
        {
            // The host already holds the authoritative copy.
            if (NetworkServer.active) return;

            GameObject masterObject = RoR2.Util.FindNetworkObject(masterId);
            if (!masterObject || !masterObject.TryGetComponent(out SkillPointsController controller))
            {
                Logger.Debug("SyncSkillPointsMessage: no controller for master {0}", masterId);
                return;
            }
            controller.ClientApplySync(survivorEnabled, earned, unspent, levelsPerSkillPoint, multScalingLinear, maxSkillLevel, levels);
        }
    }

    /// <summary>Client to host: buy one level of a skill for this master.</summary>
    internal class BuySkillMessage : INetMessage
    {
        private NetworkInstanceId masterId;
        private string skillName;

        public BuySkillMessage() { }

        internal BuySkillMessage(NetworkInstanceId masterId, string skillName)
        {
            this.masterId = masterId;
            this.skillName = skillName;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(masterId);
            writer.Write(skillName ?? string.Empty);
        }

        public void Deserialize(NetworkReader reader)
        {
            masterId = reader.ReadNetworkId();
            skillName = reader.ReadString();
        }

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            GameObject masterObject = RoR2.Util.FindNetworkObject(masterId);
            if (masterObject && masterObject.TryGetComponent(out SkillPointsController controller))
            {
                controller.ServerTryBuy(skillName);
            }
        }
    }

    /// <summary>Client to host: refund all spent skill points for this master.</summary>
    internal class RespecMessage : INetMessage
    {
        private NetworkInstanceId masterId;

        public RespecMessage() { }

        internal RespecMessage(NetworkInstanceId masterId)
        {
            this.masterId = masterId;
        }

        public void Serialize(NetworkWriter writer) => writer.Write(masterId);

        public void Deserialize(NetworkReader reader) => masterId = reader.ReadNetworkId();

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            GameObject masterObject = RoR2.Util.FindNetworkObject(masterId);
            if (masterObject && masterObject.TryGetComponent(out SkillPointsController controller))
            {
                Progression.AnnounceRespec(controller, controller.ServerTryRespec(out string reason), reason);
            }
        }
    }
}
