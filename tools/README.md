# Bisafans Pokémon importer

The importer creates Pokémon `001` through `143` from the corresponding
Bisafans Pokédex pages. It extracts German names and Pokédex text, types, base
stats, expected neutral stats at levels 50 and 100, Generation 6 level-up
attacks, and normal Generation 6 front/back sprites.

```bash
python3 -m venv /tmp/pokemon-import-venv
/tmp/pokemon-import-venv/bin/pip install -r tools/requirements.txt
/tmp/pokemon-import-venv/bin/python tools/pokemon_importer.py
```

Pages are cached under `tools/.cache/` and requests default to a 0.75-second
delay. Use `--start` and `--end` for a smaller resumable range. Generated data
is written to `data/pokemon/`, resources to `resources/pokemon/`, and sprites
to `assets/pokemon/gen6/`.

Source: <https://www.bisafans.de/pokedex/001.php>. Pokémon names, data, and
sprites remain the property of their respective owners; verify redistribution
rights before publishing generated assets.
