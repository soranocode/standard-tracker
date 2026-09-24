"""Extract deck-list art from a local Hearthstone installation.

Requires UnityPy and Pillow. Only the requested card bundles are opened; no game
files or extracted artwork are uploaded or included in the application package.
The asset lookup and crop follow HearthstoneJSON's MIT-licensed extractor:
https://github.com/Zero-to-Heroes/HearthstoneJSON-legacy/blob/master/generate_card_textures.py
"""

import argparse
from collections import OrderedDict
import os
from pathlib import Path
import re
import sys
import warnings

try:
    import UnityPy
    import UnityPy.config
    from PIL import Image, ImageOps
except ImportError as error:
    raise SystemExit(f"Local card art needs UnityPy and Pillow: {error}")


def unity_version(game_dir):
    managers = game_dir / "Hearthstone_Data" / "globalgamemanagers"
    with managers.open("rb") as source:
        match = re.search(rb"(?:20\d\d|6000)\.\d+\.\d+f\d+", source.read(1024))
    if not match:
        raise ValueError(f"Unity version not found in {managers}")
    return match.group().decode("ascii")


def asset_guid(path):
    return path.rsplit(":", 1)[-1] if path and ":" in path else None


def catalog_maps(manifest):
    data = {
        obj.read().m_Name: obj.read()
        for obj in manifest.objects
        if obj.type.name == "MonoBehaviour"
    }
    catalog = data["base_assets_catalog"]
    bundles = {item.guid: catalog.m_bundleNames[item.bundleId] for item in catalog.m_assets}
    card_map = dict(zip(data["cards_map"].map.keys, data["cards_map"].map.values))
    return bundles, card_map


def read_asset(game_data, bundles, guid, loaded):
    bundle_name = bundles.get(guid)
    if not bundle_name:
        return None
    if bundle_name not in loaded:
        bundle_path = game_data / bundle_name
        if not bundle_path.is_file():
            return None
        loaded[bundle_name] = UnityPy.load(str(bundle_path))
        if len(loaded) > 8:
            loaded.popitem(last=False)
    else:
        loaded.move_to_end(bundle_name)
    container = loaded[bundle_name].container
    pointer = container[guid] if guid in container else None
    return pointer.read() if pointer else None


def card_art_paths(prefab):
    for component in prefab.m_Component:
        if component.component.type.name != "MonoBehaviour":
            continue
        definition = component.component.read()
        portrait = getattr(definition, "m_PortraitTexturePath", "")
        if portrait:
            material = getattr(definition, "m_DeckCardBarPortraitPath", "")
            return asset_guid(portrait), asset_guid(material)
    return None, None


def tile_crop(material, dimension):
    # Unity deck bar UVs; the same rectangle HearthstoneJSON uses for its tiles.
    default = (467, 223, 440, 101, 1, 1)
    if material is None:
        return default
    sheet = material.m_SavedProperties
    tex_env = dict(sheet.m_TexEnvs).get("_MainTex")
    if tex_env is None:
        return default
    floats = dict(sheet.m_Floats)
    extra_x = floats.get("_OffsetX", 0)
    extra_y = floats.get("_OffsetY", 0)
    extra_scale = floats.get("_Scale", 1)
    offset = tex_env.m_Offset
    scale = tex_env.m_Scale
    u0 = (extra_x * extra_scale) * scale.x + offset.x
    u1 = ((1 + extra_x) * extra_scale) * scale.x + offset.x
    v0 = ((0.3856 + extra_y) * extra_scale) * scale.y + offset.y
    v1 = ((0.6144 + extra_y) * extra_scale) * scale.y + offset.y
    if u0 > u1:
        delta = u0 - u1
        u0 -= delta
        u1 += delta
    x = round(u0 * dimension)
    y = round(v0 * dimension)
    width = round(abs(u1 - u0) * dimension)
    height = round(abs(v1 - v0) * dimension)
    x = (x + width) % dimension - width
    y = (y + height) % dimension - height
    while x + width < dimension // 4:
        x += dimension
    while y + height < 0:
        y += dimension
    if x < 0:
        x += dimension
    return x, y, width, height, scale.x * extra_scale, scale.y * extra_scale


def make_tile(texture, material):
    art = texture.image.convert("RGB")
    if art.size != (512, 512):
        art = art.resize((512, 512), Image.Resampling.LANCZOS)
    x, y, width, height, flip_x, flip_y = tile_crop(material, art.width)
    if not 0 < width <= art.width * 2 or not 0 < height <= art.height:
        raise ValueError("invalid deck bar crop")
    # Repeating the source handles bars which wrap across the right edge.
    repeated = Image.new("RGB", (art.width * 2, art.height))
    repeated.paste(art, (0, 0))
    repeated.paste(art, (art.width, 0))
    y = max(0, min(y, art.height - height))
    bar = repeated.crop((x, art.height - y - height, x + width, art.height - y))
    if flip_x < 0:
        bar = ImageOps.mirror(bar)
    if flip_y < 0:
        bar = ImageOps.flip(bar)
    # The left edge is covered by the card's cost and name in the deck list.
    bar = bar.crop((min(55, bar.width - 1), 0, bar.width, bar.height))
    return bar.resize((256, 59), Image.Resampling.LANCZOS)


def extract(game_dir, output, card_ids):
    game_data = game_dir / "Data" / "Win"
    if not (game_data / "asset_manifest.unity3d").is_file():
        raise FileNotFoundError(f"Hearthstone assets not found in {game_data}")
    UnityPy.config.FALLBACK_UNITY_VERSION = unity_version(game_dir)
    warnings.filterwarnings("ignore", category=UserWarning, module="UnityPy")
    manifest = UnityPy.load(str(game_data / "asset_manifest.unity3d"))
    bundles, card_map = catalog_maps(manifest)
    output.mkdir(parents=True, exist_ok=True)
    loaded = OrderedDict()
    made = 0
    for card_id in card_ids:
        destination = output / f"{card_id}.jpg"
        if destination.is_file() and destination.stat().st_size:
            continue
        try:
            prefab_guid = asset_guid(card_map.get(card_id))
            prefab = read_asset(game_data, bundles, prefab_guid, loaded) if prefab_guid else None
            if prefab is None:
                continue
            texture_guid, material_guid = card_art_paths(prefab)
            texture = read_asset(game_data, bundles, texture_guid, loaded) if texture_guid else None
            if texture is None:
                continue
            material = read_asset(game_data, bundles, material_guid, loaded) if material_guid else None
            tile = make_tile(texture, material)
            temporary = destination.with_suffix(".jpg.tmp")
            tile.save(temporary, format="JPEG", quality=88)
            os.replace(temporary, destination)
            made += 1
        except Exception as error:
            print(f"{card_id}: {error}", file=sys.stderr)
    print(f"Extracted {made}/{len(card_ids)} card tiles from local Hearthstone files")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("card_ids", nargs="+", help="Hearthstone card IDs")
    args = parser.parse_args()
    card_ids = list(dict.fromkeys(args.card_ids))
    if any(not re.fullmatch(r"[A-Za-z0-9_]+", card_id) for card_id in card_ids):
        parser.error("card IDs may only contain letters, digits, and underscores")
    extract(args.game_dir, args.output, card_ids)


if __name__ == "__main__":
    main()
