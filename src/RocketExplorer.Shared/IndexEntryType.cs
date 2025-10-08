namespace RocketExplorer.Shared;

[Flags]
public enum IndexEntryType : byte
{
	NodeOperator = 1,

	Megapool = 2,

	MinipoolValidator = 4,

	MegapoolValidator = 8,

	RETHHolder = 16,

	RPLHolder = 32,

	RPLOldHolder = 64,

	RockRETHHolder = 128,
}