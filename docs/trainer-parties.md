# Trainer Pokémon parties

`PokemonResource` is immutable species data. A carried or opponent Pokémon is a
`PokemonInstance`, which owns its level, experience, IVs, EVs, calculated stats,
current HP, selected ability, and up to four `PokemonMoveSlot` resources.

`TrainerParty` owns at most six instances. `TrainerPartyComponent` is the shared
adapter for scene characters; it is attached to the player now and can be added
unchanged to an NPC trainer scene later.

```csharp
var component = trainer.GetNode<TrainerPartyComponent>("TrainerParty");
var species = GD.Load<PokemonResource>("res://resources/pokemon/025_pikachu.tres");
bool added = component.TryAddPokemon(species, level: 12);
```

Editor-authored opponent parties can assign a `TrainerParty` resource to the
component. Runtime additions and editor assignments both enforce the six-member
limit. Battle-only behavior for move and ability effects can be layered onto the
existing `MoveResource` reference and ability name without coupling party data to
the player.
