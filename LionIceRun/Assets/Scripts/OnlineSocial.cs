using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[Serializable] public class SocialPacket
{
    public string kind, text;
    public int emote, round;
    public ulong author, actor;
    public uint sequence;
}

// 대화는 경기 입력 채널과 분리한다. 발신자 이름과 캐릭터는 호스트 명단에서만 결정한다.
public partial class OnlineGameSession
{
    const string SocialChannel = "LionIceRun.Social.v1";
    readonly Dictionary<ulong, double> chatTimes = new(), emoteTimes = new();
    readonly Dictionary<ulong, uint> socialSequences = new();
    uint localSocialSequence;
    double nextLocalChat, nextLocalEmote;
    public event Action<string, string> ChatReceived;
    public event Action<PlayerController, int> EmoteReceived;
    void RefreshSocialBindings()
    {
        GamePreferences.RefreshButton(diveAction, 5); GamePreferences.RefreshButton(pushAction, 6);
        if (!InRoom || !registered) return;
        var member = members.Find(m => m.clientId == network.LocalClientId);
        if (member != null && member.nickname != GamePreferences.PublicName(member.skin))
            SubmitSocial(new SocialPacket { kind = "identity", text = GamePreferences.PublicName(member.skin) });
    }
    public static string CleanChat(string value)
    {
        var text = new StringBuilder();
        foreach (char c in (value ?? "").Trim())
        {
            if (char.IsControl(c) || c == '<' || c == '>') continue;
            text.Append(c); if (text.Length >= 120) break;
        }
        return text.ToString();
    }
    public bool SendChat(string text)
    {
        text = CleanChat(text);
        if (!InRoom || !registered || text.Length == 0 || ServerNow < nextLocalChat || Busy) return false;
        nextLocalChat = ServerNow + 1;
        SubmitSocial(new SocialPacket { kind = "chat", text = text }); return true;
    }
    public bool SendEmote(int emote)
    {
        if (!InRoom || !registered || !MatchPlaying || emote < 0 || emote >= 6 || ServerNow < nextLocalEmote) return false;
        nextLocalEmote = ServerNow + 1.2;
        SubmitSocial(new SocialPacket { kind = "emote", emote = emote, round = round }); return true;
    }
    void SubmitSocial(SocialPacket packet)
    {
        packet.sequence = ++localSocialSequence;
        if (IsHost) AcceptSocial(network.LocalClientId, packet);
        else SendSocial(packet, NetworkManager.ServerClientId);
    }
    void SendSocial(SocialPacket packet, ulong receiver)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
        using var writer = new FastBufferWriter(1024, Allocator.Temp);
        writer.WriteValueSafe(bytes.Length); writer.WriteBytesSafe(bytes);
        network.CustomMessagingManager.SendNamedMessage(SocialChannel, receiver, writer, NetworkDelivery.ReliableSequenced);
    }
    void OnSocial(ulong sender, FastBufferReader reader)
    {
        if (!InRoom || reader.Length > 1024) return;
        try
        {
            reader.ReadValueSafe(out int length);
            if (length < 2 || length > 900 || reader.Length - reader.Position < length) return;
            byte[] bytes = new byte[length]; reader.ReadBytesSafe(ref bytes, length);
            var packet = JsonUtility.FromJson<SocialPacket>(Encoding.UTF8.GetString(bytes));
            if (packet == null) return;
            if (IsHost) AcceptSocial(sender, packet);
            else if (sender == NetworkManager.ServerClientId) DisplaySocial(packet);
        }
        catch (Exception) { /* 잘린 메시지는 경기 흐름에 영향을 주지 않는다. */ }
    }
    void AcceptSocial(ulong sender, SocialPacket packet)
    {
        var member = members.Find(m => m.clientId == sender);
        if (member == null || (socialSequences.TryGetValue(sender, out uint old) && !OnlineRules.Newer(packet.sequence, old))) return;
        socialSequences[sender] = packet.sequence;
        packet.author = sender;
        if (packet.kind == "identity") packet.text = OnlineRules.Nickname(packet.text);
        else if (packet.kind == "chat")
        {
            packet.text = CleanChat(packet.text);
            if (packet.text.Length == 0 || (chatTimes.TryGetValue(sender, out double at) && ServerNow - at < 1)) return;
            chatTimes[sender] = ServerNow;
        }
        else if (packet.kind == "emote")
        {
            if (!MatchPlaying || packet.round != round || packet.emote < 0 || packet.emote >= 6
                || (emoteTimes.TryGetValue(sender, out double at) && ServerNow - at < 1.2)) return;
            OnlineActor owner = null;
            foreach (var actor in actors.Values) if (!actor.spawn.bot && actor.spawn.ownerId == sender) owner = actor;
            if (owner == null || !owner.gameObject.activeSelf) return;
            packet.actor = owner.spawn.actorId; emoteTimes[sender] = ServerNow;
        }
        else return;
        DisplaySocial(packet);
        foreach (ulong id in network.ConnectedClientsIds) if (id != network.LocalClientId) SendSocial(packet, id);
    }
    void DisplaySocial(SocialPacket packet)
    {
        var member = members.Find(m => m.clientId == packet.author);
        if (member == null) return;
        if (packet.kind == "identity")
        {
            member.nickname = OnlineRules.Nickname(packet.text);
            foreach (var actor in actors.Values)
                if (!actor.spawn.bot && actor.spawn.ownerId == packet.author)
                {
                    actor.spawn.nickname = member.nickname;
                    actor.GetComponent<PlayerNameplate>()?.SetName(member.nickname, actor.spawn.ownerId == network.LocalClientId, false);
                    foreach (var score in series.Scores) if (score.actorId == actor.spawn.actorId) score.nickname = member.nickname;
                }
        }
        else if (packet.kind == "chat") ChatReceived?.Invoke(member.nickname, CleanChat(packet.text));
        else if (packet.kind == "emote" && packet.round == round && actors.TryGetValue(packet.actor, out var actor))
            EmoteReceived?.Invoke(actor.controller, packet.emote);
    }
    void ClearSocial()
    {
        chatTimes.Clear(); emoteTimes.Clear(); socialSequences.Clear();
        localSocialSequence = 0; nextLocalChat = nextLocalEmote = 0;
    }
}
