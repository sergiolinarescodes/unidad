#!/usr/bin/env bash
# Every Unity file and folder in the index gets its .meta in the index too (U-11, U-47).
#   bash tools/unity-metas.sh              hook mode: write and stage missing metas (pre-commit hook, loop step)
#   bash tools/unity-metas.sh --check      verify only: missing, orphan, unknown type, duplicate GUID, guarded rename
#   bash tools/unity-metas.sh --guid <path>          print the GUID of a repo-relative path
#   bash tools/unity-metas.sh --render <key> <path>  print the rendered template
# Scope: index entries under the roots of tools/meta-roots.txt. Skipped: a segment ending in '~' (except 'Samples~',
# whose metas UPM copies), a segment starting with '.', '*.tmp'. GUID = first 32 hex of md5(repo-relative path), so
# the same path gets the same meta in every repo and parallel tasks creating one folder write identical metas.
# A missing meta takes the copy already on disk (Unity wrote it), else the template of its importer from
# tools/meta-templates/<key>.meta. A type with no template is refused: import it through the lane so Unity writes it.
# Exit 0 ok, 1 refused, 2 usage.
set -euo pipefail
root="$(git rev-parse --show-toplevel)"
cd "$root"
tpl_dir="$root/tools/meta-templates"

guid_of() { printf '%s' "$1" | md5sum | cut -c1-32; }

render() { # key path
    local f="$tpl_dir/$1.meta"
    [[ -f "$f" ]] || return 1
    sed "s/{{GUID}}/$(guid_of "$2")/" "$f" | tr -d '\r'
}

# Importer key of a file path (the template name), from its location and extension.
key_of() {
    local p="$1" name="${1##*/}" ext
    case "$name" in
        package.json) [[ "$p" =~ ^Packages/[^/]+/package\.json$ ]] && { echo package; return; } ;;
        csc.rsp|LICENSE) echo default; return ;;
    esac
    [[ "$name" == *.* ]] || { echo default; return; }
    ext="${name##*.}"; ext="${ext,,}"
    case "$ext" in
        json) if [[ "$p" == */StreamingAssets/* || "$p" == StreamingAssets/* ]]; then echo default; else echo text; fi ;;
        md|txt|bytes|xml|csv) echo text ;;
        rsp|ps1|sh) echo default ;;
        *) if [[ "$p" == */StreamingAssets/* || "$p" == StreamingAssets/* ]]; then echo default; else echo "$ext"; fi ;;
    esac
}

json_array() { # member of tools/repo.json holding a string array
    tr -d '\r\n' < "$root/tools/repo.json" | grep -o "\"$1\"[[:space:]]*:[[:space:]]*\[[^]]*\]" | grep -o '"[^"]*"' | tail -n +2 | tr -d '"' || true
}
json_string() {
    tr -d '\r\n' < "$root/tools/repo.json" | grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"[^\"]*\"" | sed 's/.*:[[:space:]]*"//; s/"$//' || true
}

case "${1:-}" in
    --guid) [[ $# -eq 2 ]] || { echo "usage: unity-metas.sh --guid <path>"; exit 2; }; guid_of "$2"; exit 0 ;;
    --render) [[ $# -eq 3 ]] || { echo "usage: unity-metas.sh --render <key> <path>"; exit 2; }
              render "$2" "$3" || { echo "unity-metas: no template $2"; exit 1; }; exit 0 ;;
    --check) mode=check ;;
    "") mode=hook ;;
    *) echo "usage: unity-metas.sh [--check | --guid <path> | --render <key> <path>]"; exit 2 ;;
esac

roots=$(sed 's/#.*//; s/\r//; s/[[:space:]]*$//; /^$/d' "$root/tools/meta-roots.txt")

# One pass over the index: MISSING <asset> <file|folder> and ORPHAN <meta> lines.
plan=$(git -c core.quotepath=off ls-files -z | tr '\0' '\n' | awk -v roots="$roots" '
    function inscope(p,   rest, n, i, seg, r) {
        for (r in R) if (index(p, R[r] "/") == 1) { rest = substr(p, length(R[r]) + 2); break }
        if (rest == "") return 0
        if (p ~ /\.tmp$/) return 0
        n = split(rest, seg, "/")
        for (i = 1; i <= n; i++) {
            if (seg[i] ~ /^\./) return 0
            if (seg[i] ~ /~$/ && (seg[i] != "Samples~" || i == n)) return 0
        }
        return R[r] "/"
    }
    BEGIN { n = split(roots, list, "\n"); for (i = 1; i <= n; i++) R[i] = list[i] }
    /\.meta$/ { metas[substr($0, 1, length($0) - 5)] = 1; metalist[++m] = $0; next }
    {
        pre = inscope($0); if (!pre) next
        files[$0] = 1
        d = $0
        while (sub(/\/[^\/]*$/, "", d) && length(d) >= length(pre)) {
            if (d ~ /~$/) continue
            folders[d] = 1
        }
    }
    END {
        for (f in files) if (!(f in metas)) print "MISSING\t" f "\tfile"
        for (d in folders) if (!(d in metas)) print "MISSING\t" d "\tfolder"
        for (i = 1; i <= m; i++) {
            a = substr(metalist[i], 1, length(metalist[i]) - 5)
            under = 0; for (r in R) if (index(a, R[r] "/") == 1) under = 1
            if (under && !(a in files) && !(a in folders)) print "ORPHAN\t" metalist[i]
        }
    }' | sort)

fail=0
while IFS=$'\t' read -r kind path what; do
    [[ -n "$kind" ]] || continue
    if [[ "$kind" == ORPHAN ]]; then
        echo "unity-metas: orphan $path; git rm --cached it or restore its asset"; fail=1; continue
    fi
    if [[ "$what" == folder ]]; then key=folder; else key=$(key_of "$path"); fi
    if [[ "$mode" == check ]]; then
        echo "unity-metas: $path has no .meta"; fail=1; continue
    fi
    if [[ -f "$path.meta" ]]; then
        git add -- "$path.meta"; echo "[unity-metas] staged $path.meta"; continue
    fi
    if ! render "$key" "$path" > "$path.meta.tmp"; then
        rm -f "$path.meta.tmp"
        echo "unity-metas: $path: no template for .${path##*.}; import it in an asset task (unity-lane.ps1 -Import) so Unity writes its meta"
        fail=1; continue
    fi
    mv "$path.meta.tmp" "$path.meta"
    git add -- "$path.meta"; echo "[unity-metas] staged $path.meta"
done <<< "$plan"

if [[ "$mode" == check ]]; then
    specs=(); while IFS= read -r r; do specs+=(":(glob)$r/**/*.meta"); done <<< "$roots"
    dups=$(git grep --cached -h -e '^guid: ' -- "${specs[@]}" 2>/dev/null | sort | uniq -d || true)
    for g in $dups; do
        [[ "$g" == guid: ]] && continue
        echo "unity-metas: duplicate guid $g in: $(git grep --cached -l -e "^guid: $g" -- "${specs[@]}" | tr '\n' ' ')"; fail=1
    done
fi

# A rename (a deleted and an added path with one file name) under a renameGuard glob breaks every YAML reference to it.
guards=$(json_array renameGuard)
if [[ -n "$guards" ]]; then
    if [[ "$mode" == hook ]]; then
        diff=$(git -c core.quotepath=off diff --cached --name-status --no-renames)
    else
        base="${UNIDAD_BASE:-origin/$(json_string mainBranch)}"
        mb=$(git merge-base HEAD "$base" 2>/dev/null || true)
        diff=$( { [[ -n "$mb" ]] && git -c core.quotepath=off diff --name-status --no-renames "$mb" HEAD; git -c core.quotepath=off diff --cached --name-status --no-renames; } || true)
    fi
    pairs=$(printf '%s\n' "$diff" | awk -F'\t' '
        $2 ~ /\.meta$/ { next }
        $1 == "D" { n = $2; sub(/.*\//, "", n); del[n] = del[n] "\n" $2 }
        $1 == "A" { n = $2; sub(/.*\//, "", n); add[n] = add[n] "\n" $2 }
        END { for (n in del) if (n in add) {
            k = split(del[n], d, "\n"); l = split(add[n], a, "\n")
            for (i = 2; i <= k; i++) for (j = 2; j <= l; j++) print d[i] "\t" a[j] } }')
    while IFS=$'\t' read -r from to; do
        [[ -n "$from" ]] || continue
        while IFS= read -r g; do
            [[ -n "$g" ]] || continue
            if [[ "$from" == $g || "$to" == $g ]]; then
                echo "unity-metas: $from -> $to renames a file under '$g'; scenes, prefabs and materials reference it by GUID (from the path): keep its path"
                fail=1; break
            fi
        done <<< "$guards"
    done <<< "$pairs"
fi

exit $fail
