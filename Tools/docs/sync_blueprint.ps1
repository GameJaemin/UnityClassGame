# blueprint.md를 읽어 blueprint.html의 내용 부분을 다시 만든다. (문서는 md만 고치고 이 스크립트를 돌린다)
# HTML의 디자인·스크립트는 그대로 두고, 표시(GEN:INTRO / GEN:TOP / GEN:OPEN / GEN:STAGES) 사이만 바꾼다.
# 실행: powershell -ExecutionPolicy Bypass -File Tools\docs\sync_blueprint.ps1
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$mdPath = Join-Path $root "blueprint.md"
$htmlPath = Join-Path $root "blueprint.html"
$utf8 = New-Object System.Text.UTF8Encoding($false)
$lines = [IO.File]::ReadAllLines($mdPath, [Text.Encoding]::UTF8)

# ---------- 글자 변환 ----------
function Esc([string]$s) { return $s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;") }
function Inline([string]$s) {
    $s = Esc $s
    $s = [regex]::Replace($s, '`([^`]+)`', '<code>$1</code>')
    $s = [regex]::Replace($s, '\*\*(.+?)\*\*', '<strong>$1</strong>')
    $s = [regex]::Replace($s, '\[([^\]]+)\]\(([^)]+)\)', '<a href="$2" target="_blank" rel="noopener">$1</a>')
    return $s
}
function JsStr([string]$s) { return '"' + $s.Replace('\', '\\').Replace('"', '\"') + '"' }
function Cells([string]$row) {
    $t = $row.Trim()
    if ($t.StartsWith("|")) { $t = $t.Substring(1) }
    if ($t.EndsWith("|")) { $t = $t.Substring(0, $t.Length - 1) }
    return ,($t.Split("|") | ForEach-Object { $_.Trim() })
}

# ---------- md 블록(표, 목록, 인용, 굵은 제목, 문단) → HTML ----------
function RenderBlocks($block) {
    $out = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $block.Count) {
        $l = $block[$i]
        if ($l.Trim() -eq "") { $i++; continue }
        if ($l.StartsWith("|")) {
            $rows = @()
            while ($i -lt $block.Count -and $block[$i].StartsWith("|")) { $rows += ,(Cells $block[$i]); $i++ }
            $head = $rows[0]; $body = $rows[2..($rows.Count - 1)]
            [void]$out.Append("<div class=`"table-scroll`"><table>")
            if ($head.Count -gt 2) {
                [void]$out.Append("<thead><tr>" + (($head | ForEach-Object { "<th>" + (Inline $_) + "</th>" }) -join "") + "</tr></thead>")
            }
            [void]$out.Append("<tbody>")
            foreach ($r in $body) {
                $sub = $r[0].StartsWith("ㄴ ")
                $first = if ($sub) { $r[0].Substring(2) } else { $r[0] }
                $cls = if ($sub) { ' class="sub-row"' } else { "" }
                $rest = ($r[1..($r.Count - 1)] | ForEach-Object { "<td>" + (Inline $_) + "</td>" }) -join ""
                [void]$out.Append("<tr><th scope=`"row`"$cls>" + (Inline $first) + "</th>$rest</tr>")
            }
            [void]$out.Append("</tbody></table></div>`n")
            continue
        }
        if ($l -match '^\d+\. ') {
            [void]$out.Append("<ol>")
            while ($i -lt $block.Count -and $block[$i] -match '^\d+\. (.*)$') { [void]$out.Append("<li>" + (Inline $Matches[1]) + "</li>"); $i++ }
            [void]$out.Append("</ol>`n")
            continue
        }
        if ($l -match '^- ') {
            [void]$out.Append("<ul>")
            while ($i -lt $block.Count -and $block[$i] -match '^- (.*)$') { [void]$out.Append("<li>" + (Inline $Matches[1]) + "</li>"); $i++ }
            [void]$out.Append("</ul>`n")
            continue
        }
        if ($l.StartsWith(">")) {
            $q = @()
            while ($i -lt $block.Count -and $block[$i].StartsWith(">")) { $q += $block[$i].TrimStart(">").Trim(); $i++ }
            [void]$out.Append("<p class=`"hint`">" + (Inline ($q -join " ")) + "</p>`n")
            continue
        }
        if ($l -match '^\*\*([^*]+)\*\*$') { [void]$out.Append("<h3>" + (Esc $Matches[1]) + "</h3>`n"); $i++; continue }
        $p = @()
        while ($i -lt $block.Count -and $block[$i].Trim() -ne "" -and -not ($block[$i] -match '^(\||- |> ?|\d+\. |\*\*[^*]+\*\*$)')) { $p += $block[$i].Trim(); $i++ }
        if ($p.Count -eq 0) { $p += $l.Trim(); $i++ }
        [void]$out.Append("<p>" + (Inline ($p -join " ")) + "</p>`n")
    }
    return $out.ToString()
}

# ---------- ## 단위로 나누기 ----------
$sections = New-Object System.Collections.ArrayList
$cur = $null
foreach ($l in $lines) {
    if ($l -match '^## (.+)$') { $cur = @{ Title = $Matches[1].Trim(); Lines = New-Object System.Collections.ArrayList }; [void]$sections.Add($cur); continue }
    if ($cur) { [void]$cur.Lines.Add($l) }
}

$intro = ""; $top = New-Object System.Text.StringBuilder; $open = ""; $stagesJs = ""
foreach ($sec in $sections) {
    $title = $sec.Title; $body = @($sec.Lines)
    if ($title -eq "개요") {
        $para = @($body | Where-Object { $_.Trim() -ne "" -and -not $_.StartsWith(">") } | ForEach-Object { $_.Trim() })
        $note = @($body | Where-Object { $_.StartsWith(">") } | ForEach-Object { $_.TrimStart(">").Trim() })
        $intro = "<p>" + (Inline ($para -join " ")) + "</p>"
        if ($note.Count) { $intro += "`n      <p class=`"hint`">" + (Inline ($note -join " ")) + "</p>" }
    }
    elseif ($title -eq "미정 사항") {
        $open = "<section class=`"open`">`n    <h2>미정 사항</h2>`n    " + (RenderBlocks $body).Trim() + "`n  </section>"
    }
    elseif ($title -eq "할 일 목록") {
        # ### 단계 → { num, title, tasks: [ {text, done, sub}, {head}, {note} ] }
        $stages = New-Object System.Collections.ArrayList
        $st = $null
        foreach ($l in $body) {
            if ($l -match '^### (\d+(?:-\d+)?)\. (.+)$') {
                $st = @{ Num = $Matches[1]; Title = $Matches[2].Trim(); Items = New-Object System.Collections.ArrayList }
                [void]$stages.Add($st); continue
            }
            if (-not $st -or $l.Trim() -eq "") { continue }
            if ($l -match '^(\s*)- \[( |x|X)\] (.+)$') {
                $item = @{ Kind = "task"; Text = (Inline $Matches[3]); Done = ($Matches[2] -ne " "); Sub = New-Object System.Collections.ArrayList }
                if ($Matches[1].Length -ge 2) {
                    $parent = $null
                    for ($k = $st.Items.Count - 1; $k -ge 0; $k--) { if ($st.Items[$k].Kind -eq "task") { $parent = $st.Items[$k]; break } }
                    if ($parent) { [void]$parent.Sub.Add($item); continue }
                }
                [void]$st.Items.Add($item); continue
            }
            if ($l -match '^\*\*([^*]+)\*\*\s*(.*)$') {
                $h = (Esc $Matches[1])
                if ($Matches[2]) { $h += " <span class=`"hint`">" + (Inline $Matches[2]) + "</span>" }
                [void]$st.Items.Add(@{ Kind = "head"; Text = $h }); continue
            }
            $t = $l.TrimStart(">").Trim()
            $last = if ($st.Items.Count) { $st.Items[$st.Items.Count - 1] } else { $null }
            if ($last -and $last.Kind -eq "note" -and $l.StartsWith(">")) { $last.Text += "<br>" + (Inline $t) }
            else { [void]$st.Items.Add(@{ Kind = "note"; Text = (Inline $t) }) }
        }
        function ItemJs($it) {
            if ($it.Kind -eq "head") { return "{ head: " + (JsStr $it.Text) + " }" }
            if ($it.Kind -eq "note") { return "{ note: " + (JsStr $it.Text) + " }" }
            $s = "{ text: " + (JsStr $it.Text) + ", done: " + $(if ($it.Done) { "true" } else { "false" })
            if ($it.Sub.Count) { $s += ", sub: [" + (($it.Sub | ForEach-Object { ItemJs $_ }) -join ", ") + "]" }
            return $s + " }"
        }
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.Append("const STAGES = [`n")
        foreach ($s in $stages) {
            [void]$sb.Append("  { num: " + (JsStr $s.Num) + ", title: " + (JsStr (Inline $s.Title)) + ", tasks: [`n")
            [void]$sb.Append((($s.Items | ForEach-Object { "    " + (ItemJs $_) }) -join ",`n"))
            [void]$sb.Append("`n  ] },`n")
        }
        [void]$sb.Append("];")
        $stagesJs = $sb.ToString()
    }
    else {
        [void]$top.Append("<section class=`"rules`">`n    <h2>" + (Esc $title) + "</h2>`n    " + (RenderBlocks $body).Trim().Replace("`n", "`n    ") + "`n  </section>`n`n  ")
    }
}

# ---------- 표시 사이 바꾸기 ----------
function Put([string]$html, [string]$open, [string]$close, [string]$content) {
    $a = $html.IndexOf($open); $b = $html.IndexOf($close)
    if ($a -lt 0 -or $b -lt $a) { throw "표시를 찾지 못함: $open" }
    return $html.Substring(0, $a + $open.Length) + $content + $html.Substring($b)
}
$html = [IO.File]::ReadAllText($htmlPath, [Text.Encoding]::UTF8)
$html = Put $html "<!-- GEN:INTRO -->" "<!-- /GEN:INTRO -->" ("`n      " + $intro + "`n      ")
$html = Put $html "<!-- GEN:TOP -->" "<!-- /GEN:TOP -->" ("`n  " + $top.ToString().TrimEnd() + "`n  ")
$html = Put $html "<!-- GEN:OPEN -->" "<!-- /GEN:OPEN -->" ("`n  " + $open + "`n  ")
$html = Put $html "// GEN:STAGES" "// /GEN:STAGES" ("`n" + $stagesJs + "`n")

# 할 일 목록이 바뀌면 브라우저에 저장된 체크를 버리고 md의 체크 상태를 따른다
$md5 = [Security.Cryptography.MD5]::Create()
$hash = -join ($md5.ComputeHash($utf8.GetBytes($stagesJs))[0..3] | ForEach-Object { $_.ToString("x2") })
$html = [regex]::Replace($html, 'const KEY = "[^"]*";', "const KEY = `"lion-tile-runner-progress-$hash`";")

[IO.File]::WriteAllText($htmlPath, $html, $utf8)
$taskCount = ([regex]::Matches($stagesJs, '\{ text: ')).Count
$doneCount = ([regex]::Matches($stagesJs, 'done: true')).Count
Write-Host "blueprint.html 갱신: 단계 $($stages.Count)개, 할 일 $doneCount / $taskCount 완료"
