﻿using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Locator;
using Skylight.Protocol.Generator;
using Skylight.Protocol.Generator.Schema;
using Skylight.Protocol.Generator.Schema.Mapping;

namespace Skylight.Protocol.Editor;

internal partial class ProtocolOverviewForm : Form
{
	private readonly string protocol;

	private readonly ProtocolSchema schema;

	private readonly List<Action> unregisterListeners;

	internal ProtocolOverviewForm(string protocol)
	{
		this.InitializeComponent();

		this.protocol = protocol;

		using (Stream stream = File.OpenRead(Path.Combine(this.protocol, "packets.json")))
		{
			this.schema = JsonSerializer.Deserialize<ProtocolSchema>(stream, ProtocolGenerator.JsonSerializerOptions)!;
		}

		this.unregisterListeners = [];
	}

	private void ProtocolOverviewFormLoad(object sender, EventArgs e)
	{
		this.protocolName.Text = Path.GetFileName(this.protocol);

		this.packetTabControl.Appearance = TabAppearance.FlatButtons;
		this.packetTabControl.SizeMode = TabSizeMode.Fixed;
		this.packetTabControl.ItemSize = new Size(0, 1);

		this.Text = $"Protocol {this.protocolName.Text}";

		foreach (string name in this.schema.Incoming.Keys)
		{
			this.incomingPacketList.Items.Add(name);
		}

		foreach (string name in this.schema.Outgoing.Keys)
		{
			this.outgoingPacketsList.Items.Add(name);
		}

		foreach (string name in this.schema.Structures.Keys)
		{
			this.structuresList.Items.Add(name);
		}

		foreach (string name in this.schema.Interfaces.Keys)
		{
			this.interfacesList.Items.Add(name);
		}
	}

	private void SelectIncomingPacket(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.packetTab;

		if (this.incomingPacketList.SelectedItems.Count != 1)
		{
			return;
		}

		string name = this.incomingPacketList.SelectedItems[0].Text;

		using MetadataLoadContext metadataLoadContext = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));

		Assembly protocolAssembly = metadataLoadContext.LoadFromAssemblyPath("Skylight.Protocol.dll");

		this.DisplayPacket(name, this.schema.Incoming[name], protocolAssembly.GetType("Skylight.Protocol.Packets.Incoming.IGameIncomingPacket")!);
	}

	private void SelectOutgoingPacket(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.packetTab;

		if (this.outgoingPacketsList.SelectedItems.Count != 1)
		{
			return;
		}

		string name = this.outgoingPacketsList.SelectedItems[0].Text;

		using MetadataLoadContext metadataLoadContext = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));

		Assembly protocolAssembly = metadataLoadContext.LoadFromAssemblyPath("Skylight.Protocol.dll");

		this.DisplayPacket(name, this.schema.Outgoing[name], protocolAssembly.GetType("Skylight.Protocol.Packets.Outgoing.IGameOutgoingPacket")!);
	}

	private void SelectStructure(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.structureTab;

		if (this.structuresList.SelectedItems.Count != 1)
		{
			return;
		}

		this.VisualizePacketDataRoot(this.structureData, this.schema.Structures[this.structuresList.SelectedItems[0].Text], null);
	}

	private void SelectInterface(object sender, EventArgs e)
	{
		this.packetTabControl.SelectedTab = this.interfaceTab;

		if (this.interfacesList.SelectedItems.Count != 1)
		{
			return;
		}

		this.interfaceData.Controls.Clear();

		foreach ((string key, string value) in this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text])
		{
			TextBox keyTextBox = new()
			{
				Width = 500,
				Text = key
			};

			TextBox valueTextBox = new()
			{
				Width = 200,
				Text = value
			};

			Button removeButton = new()
			{
				Text = "X"
			};

			string oldKey = keyTextBox.Text;
			keyTextBox.TextChanged += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(oldKey);
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;

				oldKey = keyTextBox.Text;
			};

			valueTextBox.TextChanged += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;
			};

			removeButton.Click += (_, _) =>
			{
				this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(keyTextBox.Text);

				this.interfaceData.Controls.Remove(removeButton.Parent);
			};

			this.interfaceData.Controls.Add(new FlowLayoutPanel
			{
				Width = this.interfaceData.Width - 10,
				Height = 40,
				Controls =
				{
					keyTextBox,
					valueTextBox,
					removeButton
				}
			});
		}
	}

	private void DisplayPacket(string name, PacketSchema packet, Type interfaceType)
	{
		foreach (Action unregisterListener in this.unregisterListeners)
		{
			unregisterListener();
		}

		this.unregisterListeners.Clear();

		Control packetId;
		if (this.schema.Protocol is not "Fuse")
		{
			NumericUpDown numericPacketId = new()
			{
				Maximum = 65535,
				Minimum = -1,
				Value = packet.Id is not null ? (int)packet.Id : -1
			};

			numericPacketId.ValueChanged += (_, _) => PacketIdUpdated(numericPacketId.Value < 0 ? null : (int)numericPacketId.Value);

			packetId = numericPacketId;
		}
		else
		{
			TextBox stringPacketId = new()
			{
				Text = packet.Id?.ToString()
			};

			stringPacketId.TextChanged += (_, _) => PacketIdUpdated(string.IsNullOrWhiteSpace(stringPacketId.Text) ? null : stringPacketId.Text);

			packetId = stringPacketId;
		}

		packetId.Location = new Point(71, 6);
		packetId.Name = "packetId";
		packetId.Size = new Size(120, 23);
		packetId.TabIndex = 0;

		void PacketIdUpdated(object? value)
		{
			packet.Id = value;
			packet.ImportMetadata = null;

			this.packetIdImportedFrom.Text = string.Empty;
		}

		this.packetTab.Controls.Add(packetId);

		this.unregisterListeners.Add(() => this.packetTab.Controls.Remove(packetId));

		this.packetIdImportedFrom.Text = packet.ImportMetadata?.Id ?? string.Empty;

		int groupIdentifier = name.LastIndexOf('.');

		string packetGroup = name.Substring(0, groupIdentifier);
		string packetName = name.Substring(groupIdentifier + 1);

		Type? packetInterface;
		if (interfaceType == interfaceType.Assembly.GetType("Skylight.Protocol.Packets.Incoming.IGameIncomingPacket"))
		{
			packetInterface = interfaceType.Assembly.GetType($"{interfaceType.Namespace}.{packetGroup}.I{packetName}IncomingPacket");
		}
		else if (interfaceType == interfaceType.Assembly.GetType("Skylight.Protocol.Packets.Outgoing.IGameOutgoingPacket"))
		{
			packetInterface = interfaceType.Assembly.GetType($"{interfaceType.Namespace}.{packetGroup}.{packetName}OutgoingPacket");
		}
		else
		{
			throw new Exception($"Unknown interface type {interfaceType}");
		}

		if (packet.Structure is null)
		{
			this.packetData.Controls.Clear();
			this.packetData.Controls.Add(new Label
			{
				Text = "Inherit"
			});

			return;
		}

		this.VisualizePacketDataRoot(this.packetData, packet.Structure, packetInterface!);
	}

	private void VisualizePacketDataRoot(FlowLayoutPanel panel, List<AbstractMappingSchema> structures, Type? packetInterface, bool clear = true, bool canRemove = true, List<AbstractMappingSchema>? listHook = null)
	{
		panel.SuspendLayout();

		this.VisualizePacketData(panel, structures, packetInterface, clear, canRemove, listHook);

		panel.ResumeLayout();
	}

	private void VisualizePacketData(FlowLayoutPanel panel, List<AbstractMappingSchema> structures, Type? packetInterface, bool clear = true, bool canRemove = true, List<AbstractMappingSchema>? listHook = null)
	{
		if (clear)
		{
			panel.Controls.Clear();
		}

		listHook ??= structures;

		string[] packetProperties = packetInterface?.GetProperties().Select(p => p.Name).ToArray() ?? [];

		List<Control> controls = [];
		foreach (AbstractMappingSchema structure in structures)
		{
			static ComboBox CreateTypeMapping(string type, Action<string> updateCallback)
			{
				ComboBox typeMapping = new()
				{
					Width = 200,
					SelectedText = type
				};

				typeMapping.DropDown += (_, _) =>
				{
					if (typeMapping.Items.Count == 0)
					{
						typeMapping.Items.AddRange(["string", "text", "int", "short", "bool"]);
					}
				};
				typeMapping.GotFocus += (_, _) =>
				{
					if (typeMapping.AutoCompleteSource != AutoCompleteSource.CustomSource)
					{
						typeMapping.AutoCompleteCustomSource = ["string", "text", "int", "short", "bool"];
						typeMapping.AutoCompleteSource = AutoCompleteSource.CustomSource;
						typeMapping.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
					}
				};
				typeMapping.TextChanged += (_, _) => updateCallback(typeMapping.Text);

				return typeMapping;
			}

			static TextBox CommentInput(AbstractMappingSchema mapping)
			{
				TextBox commentTextBox = new()
				{
					Width = 200,
					Text = mapping.Comment,
					PlaceholderText = "Add comment.."
				};

				commentTextBox.TextChanged += (_, _) => mapping.Comment = string.IsNullOrWhiteSpace(commentTextBox.Text) ? null : commentTextBox.Text;

				return commentTextBox;
			}

			Control control;
			if (structure is FieldMappingSchema fieldMapping)
			{
				ComboBox nameMapping = new()
				{
					Width = 200,
					SelectedText = fieldMapping.Name
				};

				nameMapping.DropDown += (_, _) =>
				{
					if (nameMapping.Items.Count == 0)
					{
						// ReSharper disable once CoVariantArrayConversion
						nameMapping.Items.AddRange(packetProperties);
					}
				};
				nameMapping.GotFocus += (_, _) =>
				{
					if (nameMapping.AutoCompleteSource != AutoCompleteSource.CustomSource)
					{
						nameMapping.AutoCompleteCustomSource = [.. packetProperties];
						nameMapping.AutoCompleteSource = AutoCompleteSource.CustomSource;
						nameMapping.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
					}
				};
				nameMapping.TextChanged += (_, _) => fieldMapping.Name = nameMapping.Text;

				control = new FlowLayoutPanel
				{
					Width = panel.Width - 10,
					Height = 40
				};

				control.Controls.AddRange([nameMapping, CreateTypeMapping(fieldMapping.Type, text => fieldMapping.Type = text), CommentInput(fieldMapping)]);
			}
			else if (structure is ConstantMappingSchema constantMapping)
			{
				TextBox valueMapping = new()
				{
					Width = 200,
					Text = constantMapping.Value
				};

				valueMapping.TextChanged += (_, _) => constantMapping.Value = valueMapping.Text;

				control = new FlowLayoutPanel
				{
					Width = panel.Width - 10,
					Height = 40
				};

				control.Controls.AddRange([CreateTypeMapping(constantMapping.Type, text => constantMapping.Type = text), valueMapping, CommentInput(constantMapping)]);
			}
			else if (structure is ConditionalMappingSchema conditionalMapping)
			{
				Label conditionalLabel = new()
				{
					Text = "When: ",
					Width = 50
				};

				TextBox conditionalTextBox = new()
				{
					Width = 500,
					Text = conditionalMapping.Condition
				};

				conditionalTextBox.TextChanged += (_, _) => conditionalMapping.Condition = conditionalTextBox.Text;

				FlowLayoutPanel conditionalLayout = new()
				{
					Location = new Point(0, 20),
					Width = panel.Width - 10
				};

				conditionalLayout.SuspendLayout();
				conditionalLayout.Controls.AddRange([conditionalLabel, conditionalTextBox, CommentInput(conditionalMapping)]);

				this.VisualizePacketData(conditionalLayout,
				[
					conditionalMapping.WhenTrue
				], null, false, false);

				conditionalLayout.ResumeLayout();

				control = new GroupBox
				{
					Width = panel.Width - 10,
					Controls =
					{
						conditionalLayout
					}
				};
			}
			else if (structure is CombineMappingSchema combineMapping)
			{
				Label combineLabel = new()
				{
					Text = "Combine to: ",
					Width = 100
				};

				FlowLayoutPanel conditionalLayout = new()
				{
					Location = new Point(0, 20),
					Width = panel.Width - 10,
					Height = 50 * combineMapping.Fields.Count
				};

				conditionalLayout.SuspendLayout();
				conditionalLayout.Controls.AddRange([combineLabel, CreateTypeMapping(combineMapping.Type, text => combineMapping.Type = text), CommentInput(combineMapping)]);

				this.VisualizePacketData(conditionalLayout, combineMapping.Fields, null, false);

				conditionalLayout.ResumeLayout();

				control = new GroupBox
				{
					Width = panel.Width - 10,
					Height = (50 * combineMapping.Fields.Count) + 40,
					Controls =
					{
						conditionalLayout
					}
				};
			}
			else
			{
				throw new NotSupportedException();
			}

			if (canRemove)
			{
				Button upButton = new()
				{
					Text = "^"
				};

				Button downButton = new()
				{
					Text = "v"
				};

				Button removeButton = new()
				{
					Text = "X"
				};

				upButton.Click += (_, _) =>
				{
					panel.Controls.SetChildIndex(control, panel.Controls.IndexOf(control) - 1);

					int index = listHook.IndexOf(structure);
					if (index == 0)
					{
						index = listHook.Count;
					}

					listHook.Remove(structure);
					listHook.Insert(index - 1, structure);
				};

				downButton.Click += (_, _) =>
				{
					panel.Controls.SetChildIndex(control, panel.Controls.IndexOf(control) + 1);

					int index = listHook.IndexOf(structure);
					if (index == listHook.Count - 1)
					{
						return;
					}

					listHook.Remove(structure);
					listHook.Insert(index + 1, structure);
				};

				removeButton.Click += (_, _) =>
				{
					listHook.Remove(structure);

					panel.Controls.Remove(control);
				};

				FlowLayoutPanel buttonsHolder = new()
				{
					Width = 300
				};

				buttonsHolder.Controls.AddRange([upButton, downButton, removeButton]);

				control.Controls.Add(buttonsHolder);
			}

			controls.Add(control);
		}

		panel.Controls.AddRange(controls.ToArray());
	}

	private void AddPacketField(object sender, EventArgs e)
	{
		FieldMappingSchema mapping = new()
		{
			Name = "NewField",
			Type = "string"
		};

		List<AbstractMappingSchema>? listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		bool clear = false;

		if (listHook is null)
		{
			if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
			{
				listHook = this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure = [];
			}
			else
			{
				listHook = this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure = [];
			}

			clear = true;
		}

		listHook.Add(mapping);

		this.VisualizePacketDataRoot(this.packetData,
		[
			mapping
		], null, clear, listHook: listHook);
	}

	private void AddPacketConstant(object sender, EventArgs e)
	{
		ConstantMappingSchema mapping = new()
		{
			Type = "string",
			Value = "New Constant"
		};

		List<AbstractMappingSchema>? listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		bool clear = false;

		if (listHook is null)
		{
			if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
			{
				listHook = this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure = [];
			}
			else
			{
				listHook = this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure = [];
			}

			clear = true;
		}

		listHook.Add(mapping);

		this.VisualizePacketDataRoot(this.packetData,
		[
			mapping
		], null, clear, listHook: listHook);
	}

	private void AddPacketConditional(object sender, EventArgs e)
	{
		ConditionalMappingSchema mapping = new()
		{
			Condition = "true",

			WhenTrue = new FieldMappingSchema
			{
				Name = "NewField",
				Type = "string"
			}
		};

		List<AbstractMappingSchema>? listHook = this.structuresTab.SelectedTab == this.incomingPacketsTab
			? this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure
			: this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure;

		bool clear = false;

		if (listHook is null)
		{
			if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
			{
				listHook = this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure = [];
			}
			else
			{
				listHook = this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure = [];
			}

			clear = true;
		}

		listHook.Add(mapping);

		this.VisualizePacketDataRoot(this.packetData,
		[
			mapping
		], null, clear, listHook: listHook);
	}

	private void AddStructureField(object sender, EventArgs e)
	{
		FieldMappingSchema mapping = new()
		{
			Name = "NewField",
			Type = "string"
		};

		this.VisualizePacketDataRoot(this.structureData,
		[
			mapping
		], null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddStructureConstant(object sender, EventArgs e)
	{
		ConstantMappingSchema mapping = new()
		{
			Type = "string",
			Value = "New Constant"
		};

		this.VisualizePacketDataRoot(this.structureData,
		[
			mapping
		], null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddStructureConditional(object sender, EventArgs e)
	{
		ConditionalMappingSchema mapping = new()
		{
			Condition = "true",

			WhenTrue = new FieldMappingSchema
			{
				Name = "NewField",
				Type = "string"
			}
		};

		this.VisualizePacketDataRoot(this.structureData,
		[
			mapping
		], null, false, listHook: this.schema.Structures[this.structuresList.SelectedItems[0].Text]);

		this.schema.Structures[this.structuresList.SelectedItems[0].Text].Add(mapping);
	}

	private void AddInterface(object sender, EventArgs e)
	{
		TextBox keyTextBox = new()
		{
			Width = 500,
			Text = "New interface"
		};

		TextBox valueTextBox = new()
		{
			Width = 200,
			Text = "New value"
		};

		Button removeButton = new()
		{
			Text = "X"
		};

		string oldKey = keyTextBox.Text;
		keyTextBox.TextChanged += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(oldKey);
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;

			oldKey = keyTextBox.Text;
		};

		valueTextBox.TextChanged += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text][keyTextBox.Text] = valueTextBox.Text;
		};

		removeButton.Click += (_, _) =>
		{
			this.schema.Interfaces[this.interfacesList.SelectedItems[0].Text].Remove(keyTextBox.Text);

			this.interfaceData.Controls.Remove(removeButton.Parent);
		};

		this.interfaceData.Controls.Add(new FlowLayoutPanel
		{
			Width = this.interfaceData.Width - 10,
			Height = 40,
			Controls =
			{
				keyTextBox,
				valueTextBox,
				removeButton
			}
		});
	}

	private async void Save(object sender, EventArgs e)
	{
		this.save.Enabled = false;

		try
		{
			string packetsTempPath = Path.Combine(this.protocol, "packets.json.temp");

			await using (Stream stream = File.OpenWrite(packetsTempPath))
			{
				await JsonSerializer.SerializeAsync(stream, this.schema, ProtocolGenerator.JsonSerializerOptions).ConfigureAwait(true);
			}

			File.Move(packetsTempPath, Path.Combine(this.protocol, "packets.json"), true);

			ProtocolSchemaResolver resolver = new(reformat: true);
			await resolver.LoadAllAsync(Path.GetDirectoryName(this.protocol)!).ConfigureAwait(true);

			try
			{
				using MetadataLoadContext metadataLoadContext = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));

				Assembly protocolAssembly = metadataLoadContext.LoadFromAssemblyPath("Skylight.Protocol.dll");

				ProtocolGenerator.Run(this.protocol, resolver.GetSchema(Path.GetFileName(this.protocol)["Skylight.Protocol.".Length..]), protocolAssembly);
			}
			catch (Exception exception)
			{
				try
				{
					if (this.BuildProtocolLibrary() is { } builtAssembly)
					{
						using MetadataLoadContext metadataLoadContext = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")));

						Assembly protocolAssembly = metadataLoadContext.LoadFromAssemblyPath(builtAssembly);

						ProtocolGenerator.Run(this.protocol, resolver.GetSchema(Path.GetFileName(this.protocol)["Skylight.Protocol.".Length..]), protocolAssembly);
					}
					else
					{
						throw;
					}
				}
				catch (Exception exception2)
				{
					if (exception == exception2)
					{
						throw;
					}

					throw new AggregateException(exception, exception2);
				}
			}

			this.BuildProtocol();
		}
		catch (Exception exception)
		{
			MessageBox.Show(exception.ToString());
		}
		finally
		{
			this.save.Enabled = true;
		}
	}

	private string? BuildProtocolLibrary()
	{
		if (!MSBuildLocator.IsRegistered)
		{
			return null;
		}

		ProjectCollection projectCollection = new();

		Project project = projectCollection.LoadProject(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(this.protocol))!, "Skylight.Protocol", "Skylight.Protocol.csproj"));

		this.PrepareProjectCompile(project, "Building the protocol library...");

		return project.GetPropertyValue("TargetPath");
	}

	private void BuildProtocol()
	{
		if (!MSBuildLocator.IsRegistered)
		{
			return;
		}

		ProjectCollection projectCollection = new();

		Project project = projectCollection.LoadProject(Path.Combine(this.protocol, $"{Path.GetFileName(this.protocol)}.csproj"));
		project.SetGlobalProperty("HotSwap", "true");

		this.PrepareProjectCompile(project, "Building the protocol...");
	}

	private void PrepareProjectCompile(Project project, string text)
	{
		Task compileTask = Task.Run(() => this.CompileProject(project));

		TaskDialogButton closeButton = TaskDialogButton.Close;
		closeButton.Enabled = false;

		TaskDialogPage taskDialogPage = new()
		{
			Heading = "Building...",
			Text = text,
			Icon = TaskDialogIcon.Information,
			ProgressBar = new TaskDialogProgressBar(TaskDialogProgressBarState.Marquee),
			Buttons =
			{
				closeButton
			}
		};

		compileTask.ContinueWith(task =>
		{
			this.BeginInvoke(() =>
			{
				closeButton.Enabled = true;
				closeButton.PerformClick();

				if (task.IsFaulted)
				{
					MessageBox.Show("Build failed: " + task.Exception);
				}
			});
		});

		TaskDialog.ShowDialog(taskDialogPage);
	}

	private void CompileProject(Project project)
	{
		CompilerOutputLogger logger = new();

		bool restore = project.Build(targets:
		[
			"Restore",
		], loggers:
		[
			logger
		]);

		if (!restore)
		{
			throw new Exception(string.Join(Environment.NewLine, logger.CompileErrors));
		}

		//Required for to be able to detect changes made by the Restore target!
		project.MarkDirty();
		project.ReevaluateIfNecessary();

		bool build = project.Build(targets:
		[
			"Build"
		], loggers:
		[
			logger
		]);

		if (!build)
		{
			throw new Exception(string.Join(Environment.NewLine, logger.CompileErrors));
		}
	}

	private void AddNew(object sender, EventArgs e)
	{
		string text = this.addNewTextBox.Text;

		if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
		{
			if (!text.Contains('.'))
			{
				MessageBox.Show("Packets require group");

				return;
			}

			bool trimInterfaceSpecifier = false;
			if (text.EndsWith("IncomingPacket"))
			{
				trimInterfaceSpecifier = true;
				text = text[..^"IncomingPacket".Length];
			}

			int nameSeparatorIndex = text.LastIndexOf('.');
			if (nameSeparatorIndex == text.Length - 1)
			{
				MessageBox.Show("Missing name");

				return;
			}
			else if (trimInterfaceSpecifier && text.Length >= nameSeparatorIndex + 3)
			{
				if (text[nameSeparatorIndex + 1] == 'I' && char.IsUpper(text[nameSeparatorIndex + 2]))
				{
					text = text.Remove(nameSeparatorIndex + 1, 1);
				}
			}

			bool result = this.schema.Incoming.TryAdd(text, new PacketSchema
			{
				Structure = []
			});

			if (!result)
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.incomingPacketList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.outgoingPacketsTab)
		{
			if (!text.Contains('.'))
			{
				MessageBox.Show("Packets require group");

				return;
			}

			if (text.EndsWith("OutgoingPacket"))
			{
				text = text[..^"OutgoingPacket".Length];
			}

			int nameSeparatorIndex = text.LastIndexOf('.');
			if (nameSeparatorIndex == text.Length - 1)
			{
				MessageBox.Show("Missing name");

				return;
			}

			bool result = this.schema.Outgoing.TryAdd(text, new PacketSchema
			{
				Structure = []
			});

			if (!result)
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.outgoingPacketsList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.selectStructureTab)
		{
			if (!this.schema.Structures.TryAdd(text, []))
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.structuresList.Items.Add(text, text, string.Empty);
		}
		else if (this.structuresTab.SelectedTab == this.interfacesTab)
		{
			if (!this.schema.Interfaces.TryAdd(text, []))
			{
				MessageBox.Show("Item already exists");

				return;
			}

			this.interfacesList.Items.Add(text, text, string.Empty);
		}

		this.addNewTextBox.Text = string.Empty;
	}

	private void KeyDownIncomingPackets(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.incomingPacketList.SelectedItems.Count == 1)
		{
			this.schema.Incoming.Remove(this.incomingPacketList.SelectedItems[0].Text);

			this.incomingPacketList.Items.Remove(this.incomingPacketList.SelectedItems[0]);
		}
	}

	private void KeyDownOutgoingPacket(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.outgoingPacketsList.SelectedItems.Count == 1)
		{
			this.schema.Outgoing.Remove(this.outgoingPacketsList.SelectedItems[0].Text);

			this.outgoingPacketsList.Items.Remove(this.outgoingPacketsList.SelectedItems[0]);
		}
	}

	private void KeyDownStructures(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.structuresList.SelectedItems.Count == 1)
		{
			this.schema.Structures.Remove(this.structuresList.SelectedItems[0].Text);

			this.structuresList.Items.Remove(this.structuresList.SelectedItems[0]);
		}
	}

	private void KeyDownInterfaces(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Delete && this.interfacesList.SelectedItems.Count == 1)
		{
			this.schema.Interfaces.Remove(this.interfacesList.SelectedItems[0].Text);

			this.interfacesList.Items.Remove(this.interfacesList.SelectedItems[0]);
		}
	}

	private void SulekDevImport(object sender, EventArgs e)
	{
		DialogResult result = this.openSulekData.ShowDialog();
		if (result == DialogResult.OK)
		{
			using FileStream fileStream = File.OpenRead(this.openSulekData.FileName);

			SulekData data = JsonSerializer.Deserialize<SulekData>(fileStream)!;

			Dictionary<string, SulekData.PacketData> incoming = [];
			foreach (SulekData.PacketData incomingPacket in data.Messages.Incoming)
			{
				incoming.Add(incomingPacket.Name, incomingPacket);
			}

			Dictionary<string, SulekData.PacketData> outgoing = [];
			foreach (SulekData.PacketData outgoingPacket in data.Messages.Outgoing)
			{
				outgoing.Add(outgoingPacket.Name, outgoingPacket);
			}

			using MetadataLoadContext metadataLoadContext = new(new PathAssemblyResolver(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll").Concat(Directory.GetFiles(Environment.CurrentDirectory, "*.dll"))));

			Assembly protocolAssembly = metadataLoadContext.LoadFromAssemblyPath("Skylight.Protocol.dll");

			foreach (Type type in protocolAssembly.GetTypes())
			{
				if (type.GetInterface("Skylight.Protocol.Packets.Incoming.IGameIncomingPacket") is not null)
				{
					string name = type.Name[1..^"IncomingPacket".Length];
					if (Check(name))
					{
						continue;
					}

					foreach (CustomAttributeData attribute in type.CustomAttributes)
					{
						if (attribute.AttributeType.Name != "AliasesAttribute")
						{
							continue;
						}

						string alternativeName = (string)attribute.ConstructorArguments[0].Value!;
						if (Check(alternativeName))
						{
							break;
						}
					}

					bool Check(string packetName)
					{
						if (outgoing.TryGetValue($"{packetName}MessageComposer", out SulekData.PacketData? outgoingPacket) || outgoing.TryGetValue($"{packetName}Composer", out outgoingPacket))
						{
							string group = type.Namespace!["Skylight.Protocol.Packets.Incoming.".Length..];

							if (this.schema.Incoming.TryGetValue($"{group}.{name}", out PacketSchema? schema))
							{
								schema.Id = outgoingPacket.Id;
								schema.ImportMetadata ??= new PacketSchema.ImportMetadataSchema();
								schema.ImportMetadata.Id = "https://sulek.dev";

								return true;
							}
						}

						return false;
					}
				}
				else if (type.GetInterface("Skylight.Protocol.Packets.Outgoing.IGameOutgoingPacket") is not null)
				{
					string name = type.Name[..^"OutgoingPacket".Length];
					if (Check(name))
					{
						continue;
					}

					foreach (CustomAttributeData attribute in type.CustomAttributes)
					{
						if (attribute.AttributeType.Name != "AliasesAttribute")
						{
							continue;
						}

						string alternativeName = (string)attribute.ConstructorArguments[0].Value!;
						if (Check(alternativeName))
						{
							break;
						}
					}

					bool Check(string packetName)
					{
						if (incoming.TryGetValue($"{packetName}MessageEvent", out SulekData.PacketData? incomingPacket) || incoming.TryGetValue($"{packetName}Event", out incomingPacket))
						{
							string group = type.Namespace!["Skylight.Protocol.Packets.Outgoing.".Length..];

							if (this.schema.Outgoing.TryGetValue($"{group}.{name}", out PacketSchema? schema))
							{
								schema.Id = incomingPacket.Id;
								schema.ImportMetadata ??= new PacketSchema.ImportMetadataSchema();
								schema.ImportMetadata.Id = "https://sulek.dev";

								return true;
							}
						}

						return false;
					}
				}
			}
		}
	}

	private void SetInherit(object sender, EventArgs e)
	{
		if (this.structuresTab.SelectedTab == this.incomingPacketsTab)
		{
			this.schema.Incoming[this.incomingPacketList.SelectedItems[0].Text].Structure = null;
		}
		else
		{
			this.schema.Outgoing[this.outgoingPacketsList.SelectedItems[0].Text].Structure = null;
		}

		this.packetData.Controls.Clear();
		this.packetData.Controls.Add(new Label
		{
			Text = "Inherit"
		});
	}

	private sealed class CompilerOutputLogger : ILogger
	{
		private readonly List<string> compileErrors = [];

		public void Initialize(IEventSource eventSource)
		{
			eventSource.ErrorRaised += (_, args) => this.AddCompileError(args);
		}

		public void Shutdown()
		{
		}

		private void AddCompileError(BuildErrorEventArgs args)
		{
			this.compileErrors.Add($"Error {args.Code} - {args.Message} in file {args.File} on line {args.LineNumber}.");
		}

		internal IReadOnlyList<string> CompileErrors => this.compileErrors;

		string ILogger.Parameters
		{
			get => string.Empty;
			set { } //Don't allow set
		}

		LoggerVerbosity ILogger.Verbosity
		{
			get => LoggerVerbosity.Diagnostic;
			set { } //Don't allow set
		}
	}
}
