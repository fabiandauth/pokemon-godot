#!/usr/bin/env python3
"""Import Pokémon #001-#143 from Bisafans into Godot resources.

The importer reads factual Pokédex data and Generation 6 level-up moves and
downloads the normal Generation 6 front/back sprites linked by each page.
Responses are cached, requests are rate-limited, and an existing output can be
resumed safely.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import time
import unicodedata
import urllib.error
import urllib.request
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from bs4 import BeautifulSoup, Tag


BASE_URL = "https://www.bisafans.de/pokedex/{number:03d}.php"
USER_AGENT = "pokemon-godot-importer/1.0 (educational game data importer)"

TYPE_MAP = {
    "Normal": 1,
    "Feuer": 2,
    "Wasser": 3,
    "Pflanze": 4,
    "Elektro": 5,
    "Eis": 6,
    "Kampf": 7,
    "Gift": 8,
    "Boden": 9,
    "Flug": 10,
    "Psycho": 11,
    "Käfer": 12,
    "Kaefer": 12,
    "Gestein": 13,
    "Geist": 14,
    "Drache": 15,
    "Unlicht": 16,
    "Stahl": 17,
    "Fee": 18,
}

STAT_MAP = {
    "KP": "hp",
    "Angriff": "attack",
    "Verteidigung": "defense",
    "Spezial-Angriff": "special_attack",
    "Spezial-Verteidigung": "special_defense",
    "Initiative": "speed",
}

RESOURCE_STATS = {
    "hp": "BaseHp",
    "attack": "BaseAttack",
    "defense": "BaseDefense",
    "special_attack": "BaseSpecialAttack",
    "special_defense": "BaseSpecialDefense",
    "speed": "BaseSpeed",
}


@dataclass
class PokemonData:
    id: int
    name: str
    level: int
    pokedex_entry: str
    pokedex_source: str
    types: list[str]
    base_stats: dict[str, int]
    expected_stats_level_50: dict[str, int]
    expected_stats_level_100: dict[str, int]
    level_up_attacks: dict[str, int]
    front_sprite_source: str
    back_sprite_source: str


def normalized_text(value: str) -> str:
    return " ".join(value.replace("\u2011", "-").replace("\u00ad", "").split())


def slug(value: str) -> str:
    ascii_name = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "_", ascii_name.lower()).strip("_")


def fetch(url: str, destination: Path, retries: int = 3) -> bytes:
    if destination.exists() and destination.stat().st_size > 0:
        return destination.read_bytes()

    destination.parent.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    for attempt in range(1, retries + 1):
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                payload = response.read()
            temporary = destination.with_suffix(destination.suffix + ".tmp")
            temporary.write_bytes(payload)
            temporary.replace(destination)
            return payload
        except (urllib.error.URLError, TimeoutError) as error:
            if attempt == retries:
                raise RuntimeError(f"Could not download {url}: {error}") from error
            time.sleep(attempt * 2)
    raise RuntimeError(f"Could not download {url}")


def section_by_heading(soup: BeautifulSoup, heading_text: str) -> Tag:
    heading = soup.find(lambda tag: isinstance(tag, Tag) and tag.name in {"h3", "h4"}
                        and normalized_text(tag.get_text()) == heading_text)
    if heading is None:
        raise ValueError(f"Missing section: {heading_text}")
    return heading


def parse_types(soup: BeautifulSoup) -> list[str]:
    infos = soup.find(id="infos")
    if not isinstance(infos, Tag):
        raise ValueError("Missing general information section")
    type_label = infos.find("dt", string=lambda text: text and normalized_text(text) == "Typ")
    type_value = type_label.find_next_sibling("dd") if isinstance(type_label, Tag) else None
    types = [image.get("alt", "").strip() for image in type_value.find_all("img")] if type_value else []
    return [pokemon_type for pokemon_type in types if pokemon_type in TYPE_MAP][:2]


def parse_stats(soup: BeautifulSoup) -> tuple[dict[str, int], dict[str, int], dict[str, int]]:
    table = section_by_heading(soup, "Statuswerte").find_next("table")
    base: dict[str, int] = {}
    expected_50: dict[str, int] = {}
    expected_100: dict[str, int] = {}
    for row in table.select("tbody tr"):
        cells = row.find_all("td", recursive=False)
        if len(cells) < 8:
            continue
        label = normalized_text(cells[0].get_text())
        key = STAT_MAP.get(label)
        if key is None:
            continue
        base[key] = int(normalized_text(cells[2].get_text()))
        expected_50[key] = int(normalized_text(cells[6].get_text()))
        expected_100[key] = int(normalized_text(cells[7].get_text()))
    if len(base) != 6:
        raise ValueError(f"Expected six stats, found {base}")
    return base, expected_50, expected_100


def parse_pokedex_entry(soup: BeautifulSoup) -> tuple[str, str]:
    pokedex = soup.find(id="pokedex")
    if not isinstance(pokedex, Tag):
        raise ValueError("Missing Pokédex entries")
    preferred_titles = ("Pokémon X", "Pokémon Y", "Pokémon Omega Rubin", "Pokémon Alpha Saphir")
    for title in preferred_titles:
        label = pokedex.find("a", title=title)
        if not isinstance(label, Tag):
            continue
        row = label.find_parent("li")
        if not isinstance(row, Tag):
            continue
        copy = BeautifulSoup(str(row), "html.parser").li
        for element in copy.select("a.label, span.label-group"):
            element.decompose()
        entry = normalized_text(copy.get_text(" ", strip=True))
        if entry and entry != "Kein Eintrag vorhanden":
            return entry, title
    raise ValueError("No Generation 6 Pokédex entry found")


def parse_level_up_attacks(soup: BeautifulSoup) -> dict[str, int]:
    generation = soup.find(id="movetable-0-gen-6")
    if not isinstance(generation, Tag):
        raise ValueError("Missing Generation 6 move table")
    heading = generation.find("h4", string=lambda text: text and "Level-Up" in text)
    table = heading.find_next("table") if isinstance(heading, Tag) else None
    if table is None:
        return {}
    attacks: dict[str, int] = {}
    for row in table.select("tbody tr"):
        cells = row.find_all("td", recursive=False)
        if len(cells) < 2:
            continue
        attack_link = cells[1].find("a")
        attack = normalized_text(attack_link.get_text()) if attack_link else normalized_text(cells[1].get_text())
        level_match = re.search(r"\d+", normalized_text(cells[0].get_text()))
        level = int(level_match.group()) if level_match else 1
        if attack:
            attacks.setdefault(attack, level)
    return attacks


def original_sprite_url(url: str) -> str:
    return url.replace("/thumbs/h120/", "/")


def parse_sprites(soup: BeautifulSoup) -> tuple[str, str]:
    generation = soup.find(id="spritetabelle--gen-6")
    first_row = generation.select_one("tbody tr") if isinstance(generation, Tag) else None
    images = first_row.find_all("img") if isinstance(first_row, Tag) else []
    if len(images) < 2:
        raise ValueError("Missing normal Generation 6 front/back sprites")
    return original_sprite_url(images[0]["src"]), original_sprite_url(images[1]["src"])


def parse_page(number: int, html: bytes) -> PokemonData:
    soup = BeautifulSoup(html, "html.parser")
    heading = soup.find("h1", string=re.compile(rf"#{number:03d}\s+"))
    if not isinstance(heading, Tag):
        raise ValueError(f"Could not find name for #{number:03d}")
    name = re.sub(r"^#\d+\s+", "", normalized_text(heading.get_text()))
    base, expected_50, expected_100 = parse_stats(soup)
    entry, entry_source = parse_pokedex_entry(soup)
    front, back = parse_sprites(soup)
    return PokemonData(
        id=number,
        name=name,
        level=1,
        pokedex_entry=entry,
        pokedex_source=entry_source,
        types=parse_types(soup),
        base_stats=base,
        expected_stats_level_50=expected_50,
        expected_stats_level_100=expected_100,
        level_up_attacks=parse_level_up_attacks(soup),
        front_sprite_source=front,
        back_sprite_source=back,
    )


def godot_value(value: Any) -> str:
    if isinstance(value, dict):
        return "{" + ", ".join(f"{json.dumps(k, ensure_ascii=False)}: {godot_value(v)}" for k, v in value.items()) + "}"
    if isinstance(value, list):
        return "[" + ", ".join(godot_value(item) for item in value) + "]"
    return json.dumps(value, ensure_ascii=False)


def write_resource(project: Path, pokemon: PokemonData) -> None:
    filename = f"{pokemon.id:03d}_{slug(pokemon.name)}.tres"
    resource_path = project / "resources" / "pokemon" / filename
    resource_path.parent.mkdir(parents=True, exist_ok=True)
    front_path = f"res://assets/pokemon/gen6/front/{pokemon.id:03d}.png"
    back_path = f"res://assets/pokemon/gen6/back/{pokemon.id:03d}.png"
    types = [TYPE_MAP[name] for name in pokemon.types]
    types += [0] * (2 - len(types))
    lines = [
        '[gd_resource type="Resource" script_class="PokemonResource" load_steps=4 format=3]',
        "",
        '[ext_resource type="Script" path="res://scripts/gameplay/pokemon/PokemonResource.cs" id="1"]',
        f'[ext_resource type="Texture2D" path="{front_path}" id="2"]',
        f'[ext_resource type="Texture2D" path="{back_path}" id="3"]',
        "",
        "[resource]",
        'script = ExtResource("1")',
        f"Name = {godot_value(pokemon.name)}",
        f"Id = {pokemon.id}",
        f"Description = {godot_value(pokemon.pokedex_entry)}",
        f"PokedexSource = {godot_value(pokemon.pokedex_source)}",
        f"DefaultLevel = {pokemon.level}",
        f"TypeOne = {types[0]}",
        f"TypeTwo = {types[1]}",
    ]
    lines.extend(f"{RESOURCE_STATS[key]} = {value}" for key, value in pokemon.base_stats.items())
    lines.extend([
        f"ExpectedStatsLevel50 = {godot_value(pokemon.expected_stats_level_50)}",
        f"ExpectedStatsLevel100 = {godot_value(pokemon.expected_stats_level_100)}",
        f"LearnableMoves = {godot_value(list(pokemon.level_up_attacks))}",
        f"LevelUpMoves = {godot_value(pokemon.level_up_attacks)}",
        'FrontSprite = ExtResource("2")',
        'BackSprite = ExtResource("3")',
        "",
    ])
    resource_path.write_text("\n".join(lines), encoding="utf-8")


def import_range(args: argparse.Namespace) -> None:
    project = Path(args.project).resolve()
    cache = project / "tools" / ".cache" / "bisafans"
    all_data: dict[str, dict[str, Any]] = {}
    for number in range(args.start, args.end + 1):
        print(f"[{number:03d}/{args.end:03d}] Fetching and parsing", flush=True)
        page_file = cache / "pages" / f"{number:03d}.html"
        was_cached = page_file.exists()
        html = fetch(BASE_URL.format(number=number), page_file)
        pokemon = parse_page(number, html)

        front = project / "assets" / "pokemon" / "gen6" / "front" / f"{number:03d}.png"
        back = project / "assets" / "pokemon" / "gen6" / "back" / f"{number:03d}.png"
        fetch(pokemon.front_sprite_source, front)
        fetch(pokemon.back_sprite_source, back)
        write_resource(project, pokemon)
        all_data[f"{number:03d}"] = asdict(pokemon)
        if not was_cached and number < args.end:
            time.sleep(args.delay)

    data_path = project / "data" / "pokemon" / "pokemon_001_143.json"
    data_path.parent.mkdir(parents=True, exist_ok=True)
    data_path.write_text(json.dumps(all_data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(all_data)} Pokémon to {data_path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--start", type=int, default=1)
    parser.add_argument("--end", type=int, default=143)
    parser.add_argument("--delay", type=float, default=0.75, help="Delay between uncached Pokédex pages")
    parser.add_argument("--project", default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    if not 1 <= args.start <= args.end <= 143:
        parser.error("range must satisfy 1 <= start <= end <= 143")
    import_range(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
