using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using KCDMerge.Core.Configuration;
using KCDMerge.Core.Data;
using Serilog;

namespace KCDMerge.Core.Services;

public class ModConflictTracker : IModConflictTracker
{
	private readonly IConflictResolver? _conflictResolver;

	private readonly IModConflictRulesService _rulesService;

	private readonly Dictionary<string, Dictionary<string, List<RowOperation>>> _fileDeltas = new Dictionary<string, Dictionary<string, List<RowOperation>>>();

	public ModConflictTracker(IConflictResolver? conflictResolver, IModConflictRulesService rulesService)
	{
		_conflictResolver = conflictResolver;
		_rulesService = rulesService ?? throw new ArgumentNullException("rulesService");
	}

	public void RegisterDelta(string fileName, string modName, DeltaResult delta)
	{
		if (delta.Type == DeltaType.NonTable)
		{
			return;
		}
		if (!_fileDeltas.ContainsKey(fileName))
		{
			_fileDeltas[fileName] = new Dictionary<string, List<RowOperation>>();
		}
		Dictionary<string, List<RowOperation>> dictionary = _fileDeltas[fileName];
		List<string> pkColumns = delta.PkColumns ?? new List<string>();
		XElement xElement = delta.DeltaDoc.Root?.Descendants("rows").FirstOrDefault();
		if (xElement == null)
		{
			return;
		}
		foreach (XElement item2 in xElement.Elements("row"))
		{
			string key = BuildRowKey(item2, pkColumns);
			string text = item2.Attribute("action")?.Value;
			RowOperation item;
			if (text == "delete")
			{
				item = new RowOperation
				{
					Type = RowOpType.Delete,
					ModName = modName,
					Row = item2,
					PkColumns = pkColumns
				};
			}
			else if (text == "modify")
			{
				Dictionary<string, string> changedAttrs = (from a in item2.Attributes()
					where a.Name.LocalName != "action" && !pkColumns.Contains<string>(a.Name.LocalName, StringComparer.OrdinalIgnoreCase)
					select a).ToDictionary((XAttribute a) => a.Name.LocalName, (XAttribute a) => a.Value);
				item = new RowOperation
				{
					Type = RowOpType.Modify,
					ModName = modName,
					Row = item2,
					ChangedAttrs = changedAttrs,
					PkColumns = pkColumns
				};
			}
			else
			{
				item = new RowOperation
				{
					Type = RowOpType.Add,
					ModName = modName,
					Row = item2,
					PkColumns = pkColumns
				};
			}
			if (!dictionary.ContainsKey(key))
			{
				dictionary[key] = new List<RowOperation>();
			}
			dictionary[key].Add(item);
		}
		Log.Debug("[CONFLICT-TRACKER] Registered delta for {FileName} from {ModName}: {RowCount} rows", fileName, modName, xElement.Elements("row").Count());
	}

	public async Task<ResolvedMergeOps> ResolveConflictsAsync(string fileName)
	{
		if (!_fileDeltas.ContainsKey(fileName))
		{
			return new ResolvedMergeOps();
		}
		List<ConflictRecord> conflicts = DetectConflicts(fileName);
		if (conflicts.Count > 0)
		{
			Log.Information("[CONFLICT-TRACKER] {FileName}: Detected {Count} conflicts", fileName, conflicts.Count);
		}
		ResolvedMergeOps result = new ResolvedMergeOps
		{
			ResolvedConflicts = conflicts
		};
		Dictionary<string, List<RowOperation>> dictionary = _fileDeltas[fileName];
		foreach (var (rowKey, list2) in dictionary)
		{
			if (list2.Count == 1)
			{
				ApplySingleOperation(list2[0], result);
			}
			else
			{
				await ResolveMultipleOperations(rowKey, list2, result, conflicts);
			}
		}
		return result;
	}

	private List<ConflictRecord> DetectConflicts(string fileName)
	{
		List<ConflictRecord> list = new List<ConflictRecord>();
		foreach (var (rowKey, list3) in _fileDeltas[fileName])
		{
			if (list3.Count <= 1)
			{
				continue;
			}
			for (int i = 0; i < list3.Count; i++)
			{
				for (int j = i + 1; j < list3.Count; j++)
				{
					RowOperation rowOperation = list3[i];
					RowOperation rowOperation2 = list3[j];
					string text2 = null;
					if ((rowOperation.Type == RowOpType.Add && rowOperation2.Type == RowOpType.Delete) || (rowOperation.Type == RowOpType.Delete && rowOperation2.Type == RowOpType.Add))
					{
						text2 = "AddVsDelete";
					}
					else if ((rowOperation.Type == RowOpType.Modify && rowOperation2.Type == RowOpType.Delete) || (rowOperation.Type == RowOpType.Delete && rowOperation2.Type == RowOpType.Modify))
					{
						text2 = "ModifyVsDelete";
					}
					else if (rowOperation.Type == RowOpType.Modify && rowOperation2.Type == RowOpType.Modify)
					{
						if (rowOperation.ChangedAttrs != null && rowOperation2.ChangedAttrs != null)
						{
							foreach (string item in rowOperation.ChangedAttrs.Keys.Intersect(rowOperation2.ChangedAttrs.Keys))
							{
								if (rowOperation.ChangedAttrs[item] != rowOperation2.ChangedAttrs[item])
								{
									text2 = "ModifyVsModify";
									break;
								}
							}
						}
					}
					else if (rowOperation.Type == RowOpType.Add && rowOperation2.Type == RowOpType.Add && rowOperation.Row.ToString() != rowOperation2.Row.ToString())
					{
						text2 = "AddVsAdd";
					}
					if (text2 != null)
					{
						list.Add(new ConflictRecord
						{
							RowKey = rowKey,
							ModA = rowOperation.ModName,
							ModB = rowOperation2.ModName,
							ConflictType = text2
						});
					}
				}
			}
		}
		return list;
	}

	private void ApplySingleOperation(RowOperation op, ResolvedMergeOps result)
	{
		switch (op.Type)
		{
		case RowOpType.Add:
			result.RowsToAdd.Add(new XElement(op.Row));
			break;
		case RowOpType.Delete:
			result.RowsToDelete.Add(new XElement(op.Row));
			break;
		case RowOpType.Modify:
			if (op.ChangedAttrs != null)
			{
				result.RowsToModify.Add(new RowModification
				{
					RowKey = BuildRowKey(op.Row, op.PkColumns),
					AttributeChanges = new Dictionary<string, string>(op.ChangedAttrs),
					WinningMod = op.ModName
				});
			}
			break;
		}
	}

	private async Task ResolveMultipleOperations(string rowKey, List<RowOperation> ops, ResolvedMergeOps result, List<ConflictRecord> conflicts)
	{
		List<ConflictRecord> conflictsForRow = conflicts.Where((ConflictRecord c) => c.RowKey == rowKey).ToList();
		if (conflictsForRow.Count == 0)
		{
			foreach (RowOperation op in ops)
			{
				ApplySingleOperation(op, result);
			}
			return;
		}
		Dictionary<string, string> winners = new Dictionary<string, string>();
		var list = conflictsForRow.Select((ConflictRecord c) => new { c.ModA, c.ModB }).Distinct().ToList();
		foreach (var pair in list)
		{
			string[] array = new string[2] { pair.ModA, pair.ModB }.OrderBy((string m) => m).ToArray();
			string canonicalKey = array[0] + "|" + array[1];
			if (!winners.ContainsKey(canonicalKey))
			{
				string xmlConflictWinner = _rulesService.GetXmlConflictWinner(pair.ModA, pair.ModB);
				if (xmlConflictWinner != null)
				{
					winners[canonicalKey] = xmlConflictWinner;
				}
				else if (_conflictResolver != null)
				{
					int value = conflictsForRow.Count((ConflictRecord c) => (c.ModA == pair.ModA && c.ModB == pair.ModB) || (c.ModA == pair.ModB && c.ModB == pair.ModA));
					string conflictSummary = $"{value} row conflict(s)";
					string text2 = (winners[canonicalKey] = await _conflictResolver.ResolveModConflictAsync(pair.ModA, pair.ModB, conflictSummary));
					_rulesService.SetXmlConflictWinner(text2, (text2 == pair.ModA) ? pair.ModB : pair.ModA);
				}
				else
				{
					winners[canonicalKey] = pair.ModA;
				}
			}
			foreach (ConflictRecord item in conflictsForRow.Where((ConflictRecord c) => (c.ModA == pair.ModA && c.ModB == pair.ModB) || (c.ModA == pair.ModB && c.ModB == pair.ModA)))
			{
				item.Winner = winners[canonicalKey];
			}
		}
		foreach (RowOperation item2 in ops.Where(delegate(RowOperation op)
		{
			IEnumerable<ConflictRecord> source = conflictsForRow.Where((ConflictRecord c) => c.ModA == op.ModName || c.ModB == op.ModName);
			return !source.Any() || source.All((ConflictRecord c) => c.Winner == op.ModName);
		}).ToList())
		{
			ApplySingleOperation(item2, result);
		}
	}

	private string BuildRowKey(XElement row, List<string> pkColumns)
	{
		if (pkColumns == null || pkColumns.Count == 0)
		{
			return RowSetComparer.BuildRowHash(row);
		}
		List<string> list = new List<string>();
		foreach (string pkColumn in pkColumns)
		{
			string text = row.Attribute(pkColumn)?.Value ?? "";
			list.Add(pkColumn + "=" + text.ToLowerInvariant());
		}
		return string.Join("|", list);
	}
}
