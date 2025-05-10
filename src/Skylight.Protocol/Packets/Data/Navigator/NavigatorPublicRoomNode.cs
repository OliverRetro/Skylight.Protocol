﻿using System.Diagnostics.CodeAnalysis;

namespace Skylight.Protocol.Packets.Data.Navigator;

public sealed class NavigatorPublicRoomNode : NavigatorNodeData
{
	public required string Name { get; init; }

	public required int InstanceId { get; init; }
	public required int WorldId { get; init; }

	public required string Host { get; init; }
	public required int Port { get; init; }

	public required string Casts { get; init; }

	public NavigatorPublicRoomNode()
	{
	}

	[SetsRequiredMembers]
	public NavigatorPublicRoomNode(int id, int parentId, string caption, int userCount, int usersMax, string name, int instanceId, int worldId, string host, int port, string casts)
		: base(id, parentId, caption, userCount, usersMax)
	{
		this.Name = name;
		this.InstanceId = instanceId;
		this.WorldId = worldId;
		this.Host = host;
		this.Port = port;
		this.Casts = casts;
	}
}
