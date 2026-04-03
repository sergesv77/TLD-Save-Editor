#!/usr/bin/env python3
"""
Developer utility for inspecting The Long Dark save/profile files.

Examples:
  python3 scripts/inspect_tld_save.py summary ../TheLongDark/story6
  python3 scripts/inspect_tld_save.py dump ../TheLongDark/story6 --section boot
  python3 scripts/inspect_tld_save.py dump ../TheLongDark/story6 --section global/m_Inventory_Serialized
  python3 scripts/inspect_tld_save.py compare ../TheLongDark/story6 ../TheLongDark/Survival/sandbox1 --section global
"""

import argparse
import base64
import json
import sys
from pathlib import Path


def lzf_decompress(data):
    output_len = max(1, len(data) * 2)
    while True:
        output = bytearray(output_len)
        input_index = 0
        output_index = 0
        ok = True
        try:
            while input_index < len(data):
                ctrl = data[input_index]
                input_index += 1

                if ctrl < 32:
                    ctrl += 1
                    if output_index + ctrl > output_len:
                        ok = False
                        break
                    output[output_index:output_index + ctrl] = data[input_index:input_index + ctrl]
                    input_index += ctrl
                    output_index += ctrl
                    continue

                length = ctrl >> 5
                reference = output_index - ((ctrl & 0x1F) << 8) - 1
                if length == 7:
                    length += data[input_index]
                    input_index += 1
                reference -= data[input_index]
                input_index += 1

                if output_index + length + 2 > output_len or reference < 0:
                    ok = False
                    break

                output[output_index] = output[reference]
                output_index += 1
                reference += 1
                output[output_index] = output[reference]
                output_index += 1
                reference += 1

                while length:
                    output[output_index] = output[reference]
                    output_index += 1
                    reference += 1
                    length -= 1
        except IndexError:
            ok = False

        if ok:
            return bytes(output[:output_index])
        output_len *= 2


def load_lzf_json(path):
    raw = path.read_bytes()
    decompressed = lzf_decompress(raw)
    text = decompressed.decode("utf-8")
    return json.loads(text), raw, decompressed


def looks_like_json_text(value):
    if not isinstance(value, str):
        return False
    stripped = value.lstrip()
    return stripped.startswith("{") or stripped.startswith("[")


def try_parse_json_string(value):
    if looks_like_json_text(value):
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value
    return value


def is_int_list(value):
    return isinstance(value, list) and all(isinstance(item, int) and 0 <= item <= 255 for item in value)


def decode_slot_blob(value):
    if isinstance(value, str):
        data = base64.b64decode(value)
    elif is_int_list(value):
        data = bytes(value)
    elif isinstance(value, (bytes, bytearray)):
        data = bytes(value)
    else:
        raise TypeError(f"Unsupported blob type: {type(value).__name__}")

    decoded = lzf_decompress(data)
    text = decoded.decode("utf-8")
    return json.loads(text)


def resolve_section(root_object, section):
    if not section or section == "root":
        return root_object

    current = root_object
    for segment in section.split("/"):
        if not segment:
            continue

        if (
            isinstance(current, dict)
            and "m_Dict" in current
            and segment in current["m_Dict"]
        ):
            current = decode_slot_blob(current["m_Dict"][segment])
            continue

        if not isinstance(current, dict) or segment not in current:
            raise KeyError(f"Could not resolve section segment '{segment}'")

        current = try_parse_json_string(current[segment])

    return current


def print_json(data):
    json.dump(data, sys.stdout, indent=2, sort_keys=True)
    sys.stdout.write("\n")


def describe_value(value):
    if isinstance(value, dict):
        return f"dict[{len(value)}]"
    if isinstance(value, list):
        return f"list[{len(value)}]"
    if isinstance(value, str):
        return "str"
    if isinstance(value, bool):
        return "bool"
    if value is None:
        return "null"
    return type(value).__name__


def print_summary(path, section):
    root_object, raw, decompressed = load_lzf_json(path)
    target = resolve_section(root_object, section)

    print(f"path: {path}")
    print(f"compressed_bytes: {len(raw)}")
    print(f"decompressed_bytes: {len(decompressed)}")
    print(f"section: {section}")
    print(f"section_type: {describe_value(target)}")

    if section == "root" and isinstance(root_object, dict):
        meta_keys = [
            "m_InternalName",
            "m_Name",
            "m_BaseName",
            "m_DisplayName",
            "m_GameMode",
            "m_Episode",
            "m_Timestamp",
            "m_Version",
            "m_Changelist",
        ]
        metadata = {key: root_object[key] for key in meta_keys if key in root_object}
        if metadata:
            print("metadata:")
            print_json(metadata)

    if isinstance(target, dict):
        print(f"key_count: {len(target)}")
        print("keys:")
        for key in target.keys():
            print(f"- {key} ({describe_value(target[key])})")

        if section == "root" and "m_Dict" in target and isinstance(target["m_Dict"], dict):
            print("slot_dict_keys:")
            for key, value in target["m_Dict"].items():
                print(f"- {key} ({describe_value(value)})")
    elif isinstance(target, list):
        print(f"list_length: {len(target)}")
        if target:
            print(f"first_item_type: {describe_value(target[0])}")
    else:
        print("value:")
        print(target)


def compare_sections(path_a, path_b, section):
    object_a = resolve_section(load_lzf_json(path_a)[0], section)
    object_b = resolve_section(load_lzf_json(path_b)[0], section)

    if not isinstance(object_a, dict) or not isinstance(object_b, dict):
        raise TypeError("Compare currently supports only sections that resolve to JSON objects")

    keys_a = set(object_a.keys())
    keys_b = set(object_b.keys())

    print(f"section: {section}")
    print(f"{path_a.name} key_count: {len(keys_a)}")
    print(f"{path_b.name} key_count: {len(keys_b)}")

    only_a = sorted(keys_a - keys_b)
    only_b = sorted(keys_b - keys_a)

    print(f"only_in_{path_a.name}:")
    for key in only_a:
        print(f"- {key}")

    print(f"only_in_{path_b.name}:")
    for key in only_b:
        print(f"- {key}")


def build_parser():
    parser = argparse.ArgumentParser(description="Inspect The Long Dark save/profile files")
    subparsers = parser.add_subparsers(dest="command", required=True)

    summary_parser = subparsers.add_parser("summary", help="Print a concise summary of a decoded file/section")
    summary_parser.add_argument("path", type=Path)
    summary_parser.add_argument("--section", default="root")

    dump_parser = subparsers.add_parser("dump", help="Dump a decoded JSON section")
    dump_parser.add_argument("path", type=Path)
    dump_parser.add_argument("--section", default="root")

    compare_parser = subparsers.add_parser("compare", help="Compare object keys between two files/sections")
    compare_parser.add_argument("path_a", type=Path)
    compare_parser.add_argument("path_b", type=Path)
    compare_parser.add_argument("--section", default="root")

    return parser


def main():
    parser = build_parser()
    args = parser.parse_args()

    if args.command == "summary":
        print_summary(args.path, args.section)
        return 0

    if args.command == "dump":
        data = resolve_section(load_lzf_json(args.path)[0], args.section)
        print_json(data)
        return 0

    if args.command == "compare":
        compare_sections(args.path_a, args.path_b, args.section)
        return 0

    parser.error(f"Unsupported command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
