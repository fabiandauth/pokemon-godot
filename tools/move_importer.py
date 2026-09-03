#!/usr/bin/env python3
"""Import Generation 1-6 moves from PokéWiki into JSON and Godot resources.

The importer uses PokéWiki's MediaWiki API instead of scraping the public HTML
URLs directly. Responses are cached, requests are rate-limited, duplicate moves
are merged by canonical name, and status moves receive a plain-text description
from the move page's Effect section.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from bs4 import BeautifulSoup, Tag


API_URL = "https://pokewiki.de/api.php"
ARTICLE_URL = "https://www.pokewiki.de/"
USER_AGENT = "pokemon-godot-move-importer/1.0 (educational game data importer)"

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
    "Gestein": 13,
    "Geist": 14,
    "Drache": 15,
    "Unlicht": 16,
    "Stahl": 17,
    "Fee": 18,
}

CATEGORY_MAP = {"Physisch": 0, "Spezial": 1, "Status": 2}


@dataclass
class MoveData:
    id: int
    name: str
    aliases: list[str]
    generation: int
    type: str
    category: str
    power: int | None
    accuracy: int | None
    pp: int
    status_change: str
    source_page: str
    source_url: str


def normalized_text(value: str) -> str:
    return " ".join(value.replace("\u2011", "-").replace("\u00ad", "").split())


def normalized_name(value: str) -> str:
    return unicodedata.normalize("NFC", normalized_text(value)).casefold()


def normalized_prose(value: str) -> str:
    text = normalized_text(value)
    text = re.sub(r"\s+([,.;:!?])", r"\1", text)
    return re.sub(r"(?<=\w)\s+-\s+(?=\w)", "-", text)


def slug(value: str) -> str:
    ascii_name = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "_", ascii_name.lower()).strip("_")


class WikiClient:
    def __init__(self, cache: Path, delay: float, refresh: bool) -> None:
        self.cache = cache
        self.delay = delay
        self.refresh = refresh
        self.last_request = 0.0

    def parse_page(self, title: str) -> tuple[str, str]:
        digest = hashlib.sha1(title.encode("utf-8")).hexdigest()[:12]
        cache_path = self.cache / f"{slug(title) or 'page'}_{digest}.json"
        if cache_path.exists() and not self.refresh:
            payload = json.loads(cache_path.read_text(encoding="utf-8"))
        else:
            elapsed = time.monotonic() - self.last_request
            if elapsed < self.delay:
                time.sleep(self.delay - elapsed)
            query = urllib.parse.urlencode({
                "action": "parse",
                "page": title,
                "prop": "text",
                "format": "json",
                "formatversion": 2,
                "redirects": 1,
            })
            request = urllib.request.Request(
                f"{API_URL}?{query}",
                headers={"User-Agent": USER_AGENT, "Accept": "application/json"},
            )
            payload = self._request_json(request)
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            temporary = cache_path.with_suffix(".tmp")
            temporary.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
            temporary.replace(cache_path)
            self.last_request = time.monotonic()

        if "error" in payload:
            raise RuntimeError(f"PokéWiki API error for {title}: {payload['error']}")
        parsed = payload["parse"]
        return parsed.get("title", title), parsed["text"]

    @staticmethod
    def _request_json(request: urllib.request.Request, retries: int = 4) -> dict[str, Any]:
        for attempt in range(1, retries + 1):
            try:
                with urllib.request.urlopen(request, timeout=60) as response:
                    return json.loads(response.read().decode("utf-8"))
            except (urllib.error.URLError, TimeoutError, json.JSONDecodeError) as error:
                if attempt == retries:
                    raise RuntimeError(f"Could not download {request.full_url}: {error}") from error
                time.sleep(attempt * 2)
        raise RuntimeError(f"Could not download {request.full_url}")


def numeric_value(value: str) -> int | None:
    match = re.search(r"\d+", normalized_text(value))
    return int(match.group()) if match else None


def icon_title(cell: Tag, class_name: str) -> str:
    icon = cell.select_one(f".{class_name}[title]")
    if isinstance(icon, Tag):
        return normalized_text(str(icon.get("title", "")))
    return normalized_text(cell.get_text(" ", strip=True))


def find_move_table(soup: BeautifulSoup) -> Tag:
    for table in soup.find_all("table"):
        headers = {normalized_text(cell.get_text(" ", strip=True)) for cell in table.find_all("th")}
        if {"ID", "Name", "Typ", "Kategorie", "Stärke", "AP"}.issubset(headers):
            return table
    raise ValueError("Could not find the move table")


def move_names(cell: Tag) -> tuple[str, list[str], str]:
    candidates: list[tuple[str, str]] = []
    for link in cell.find_all("a"):
        text = normalized_text(link.get_text(" ", strip=True))
        title = normalized_text(str(link.get("title", "")))
        if not text or "generation" in title.casefold() or title.startswith("Kategorie:"):
            continue
        if normalized_name(text) not in {normalized_name(name) for name, _ in candidates}:
            candidates.append((text, title or text))
    if not candidates:
        name = normalized_text(cell.get_text(" ", strip=True))
        if not name:
            raise ValueError("Move row has no name")
        return name, [], name

    name, source_page = candidates[-1]
    aliases = [candidate for candidate, _ in candidates[:-1] if normalized_name(candidate) != normalized_name(name)]
    return name, aliases, source_page


def parse_generation(generation: int, html: str) -> list[MoveData]:
    soup = BeautifulSoup(html, "html.parser")
    table = find_move_table(soup)
    moves: list[MoveData] = []
    for row in table.find_all("tr"):
        cells = row.find_all(["td", "th"], recursive=False)
        if len(cells) < 7:
            continue
        move_id = numeric_value(cells[0].get_text(" ", strip=True))
        if move_id is None:
            continue
        name, aliases, source_page = move_names(cells[1])
        move_type = icon_title(cells[2], "typ-icon")
        category = icon_title(cells[3], "kategorie-icon")
        if move_type not in TYPE_MAP:
            raise ValueError(f"Unknown type {move_type!r} for {name}")
        if category not in CATEGORY_MAP:
            raise ValueError(f"Unknown category {category!r} for {name}")
        moves.append(MoveData(
            id=move_id,
            name=name,
            aliases=aliases,
            generation=generation,
            type=move_type,
            category=category,
            power=numeric_value(cells[4].get_text(" ", strip=True)),
            accuracy=numeric_value(cells[5].get_text(" ", strip=True)),
            pp=numeric_value(cells[6].get_text(" ", strip=True)) or 0,
            status_change="",
            source_page=source_page,
            source_url=ARTICLE_URL + urllib.parse.quote(source_page.replace(" ", "_"), safe="()'_-"),
        ))
    if not moves:
        raise ValueError(f"No moves found for Generation {generation}")
    return moves


def status_change_from_page(html: str) -> str:
    soup = BeautifulSoup(html, "html.parser")
    marker = soup.find(id=lambda value: isinstance(value, str) and normalized_name(value) == "effekt")
    if not isinstance(marker, Tag):
        return ""

    section = marker
    if marker.name not in {"h2", "h3", "h4"}:
        heading = marker.find_parent(["h2", "h3", "h4"])
        section = heading if isinstance(heading, Tag) else marker
    if isinstance(section.parent, Tag) and "mw-heading" in section.parent.get("class", []):
        section = section.parent

    for sibling in section.find_next_siblings():
        if sibling.name in {"h2", "h3", "h4"} or "mw-heading" in sibling.get("class", []):
            break
        paragraphs = [sibling] if sibling.name == "p" else sibling.find_all("p")
        for paragraph in paragraphs:
            text = normalized_prose(paragraph.get_text(" ", strip=True))
            if text:
                return text
    return ""


def merge_moves(moves: list[MoveData]) -> list[MoveData]:
    unique: dict[str, MoveData] = {}
    for move in moves:
        key = normalized_name(move.name)
        existing = unique.get(key)
        if existing is None:
            unique[key] = move
            continue
        aliases = existing.aliases + move.aliases + ([move.name] if move.name != existing.name else [])
        existing.aliases = list(dict.fromkeys(alias for alias in aliases if alias != existing.name))
        existing.generation = min(existing.generation, move.generation)
    return sorted(unique.values(), key=lambda move: move.id)


def godot_value(value: Any) -> str:
    if isinstance(value, list):
        return "[" + ", ".join(godot_value(item) for item in value) + "]"
    return json.dumps(value, ensure_ascii=False)


def write_resource(project: Path, move: MoveData) -> None:
    resource_path = project / "resources" / "moves" / f"{move.id:03d}_{slug(move.name)}.tres"
    resource_path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        '[gd_resource type="Resource" script_class="MoveResource" load_steps=2 format=3]',
        "",
        '[ext_resource type="Script" path="res://scripts/gameplay/moves/MoveResource.cs" id="1"]',
        "",
        "[resource]",
        'script = ExtResource("1")',
        f"Name = {godot_value(move.name)}",
        f"Aliases = {godot_value(move.aliases)}",
        f"Generation = {move.generation}",
        f"SourceUrl = {godot_value(move.source_url)}",
        f"Category = {CATEGORY_MAP[move.category]}",
        f"PokemonType = {TYPE_MAP[move.type]}",
        f"Accuracy = {move.accuracy or 0}",
        f"Power = {move.power or 0}",
        f"PP = {move.pp}",
        f"StatusChange = {godot_value(move.status_change)}",
        "",
    ]
    resource_path.write_text("\n".join(lines), encoding="utf-8")


def import_moves(args: argparse.Namespace) -> None:
    project = Path(args.project).resolve()
    client = WikiClient(project / "tools" / ".cache" / "pokewiki" / "moves", args.delay, args.refresh)
    collected: list[MoveData] = []
    for generation in range(args.start_generation, args.end_generation + 1):
        title = f"Attacken der {generation}. Generation"
        print(f"[Generation {generation}] Fetching move list", flush=True)
        _, html = client.parse_page(title)
        generation_moves = parse_generation(generation, html)
        collected.extend(generation_moves)
        print(f"[Generation {generation}] Found {len(generation_moves)} moves", flush=True)

    moves = merge_moves(collected)
    status_moves = [move for move in moves if move.category == "Status"]
    if not args.skip_status_details:
        for index, move in enumerate(status_moves, 1):
            print(f"[Status {index}/{len(status_moves)}] {move.name}", flush=True)
            canonical_title, html = client.parse_page(move.source_page)
            move.source_page = canonical_title
            move.source_url = ARTICLE_URL + urllib.parse.quote(canonical_title.replace(" ", "_"), safe="()'_-")
            move.status_change = status_change_from_page(html)

    output = {f"{move.id:03d}": asdict(move) for move in moves}
    data_path = project / "data" / "moves" / "moves_gen_1_6.json"
    data_path.parent.mkdir(parents=True, exist_ok=True)
    data_path.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for move in moves:
        write_resource(project, move)
    print(f"Wrote {len(moves)} unique moves ({len(status_moves)} status) to {data_path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--start-generation", type=int, default=1)
    parser.add_argument("--end-generation", type=int, default=6)
    parser.add_argument("--delay", type=float, default=0.25, help="Delay between uncached API requests")
    parser.add_argument("--project", default=Path(__file__).resolve().parents[1])
    parser.add_argument("--refresh", action="store_true", help="Ignore cached API responses")
    parser.add_argument("--skip-status-details", action="store_true", help="Do not follow status move pages")
    args = parser.parse_args()
    if not 1 <= args.start_generation <= args.end_generation <= 6:
        parser.error("generation range must satisfy 1 <= start <= end <= 6")
    if args.delay < 0:
        parser.error("delay must not be negative")
    import_moves(args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
