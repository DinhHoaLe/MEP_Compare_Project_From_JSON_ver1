Add-Type -AssemblyName System.Drawing
$r = Get-Content "$PSScriptRoot\comparison.json" -Raw | ConvertFrom-Json
$d = Get-Content "$PSScriptRoot\region.json" -Raw | ConvertFrom-Json
$bmp = New-Object System.Drawing.Bitmap 1500,1040
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font 'Segoe UI',12
$small = New-Object System.Drawing.Font 'Segoe UI',10
$title = New-Object System.Drawing.Font 'Segoe UI',21,([System.Drawing.FontStyle]::Bold)
$dark = [System.Drawing.Brushes]::MidnightBlue
$g.DrawString('E04_HoL | RLT 05-01 | XYZ screening: 10 mm',$title,$dark,35,22)
$g.DrawString('Read from the open Revit 2019 model and linked CAD. No cloud or model geometry changes.',$font,$dark,35,67)
$g.DrawString('PLAN: 9 sample straight ducts inside an 8 x 8 m window',$font,$dark,35,111)
$green = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(15,130,85)),3
$red = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(211,62,54)),3
$grey = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210,216,223)),1
$blue = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(35,104,205)),2
function PX([double]$x) {return [single](55+($x-14529000)*0.065)}
function PY([double]$y) {return [single](690-($y-5214000)*0.065)}
for($i=0;$i -le 8;$i++) {$xx=[single](55+$i*65);$yy=[single](170+$i*65);$g.DrawLine($grey,$xx,170,$xx,690);$g.DrawLine($grey,55,$yy,575,$yy)}
$index=0
foreach($e in $r.results){
 $index++;$x0=PX ([Math]::Min($e.centerline[0][0],$e.centerline[1][0]));$x1=PX ([Math]::Max($e.centerline[0][0],$e.centerline[1][0]));$yy=PY $e.centerline[0][1];$half=[single]($e.width*0.065/2)
 $pen=if($e.coveragePercent -eq 100){$green}else{$red}
 $g.DrawRectangle($pen,$x0,($yy-$half),($x1-$x0),($half*2))
 $g.DrawString("$index",$small,$dark,($x0+3),($yy-8))
 $vals=($e.maxAbsXYZ | ForEach-Object {([double]$_).ToString('0.00')}) -join ' / '
 $g.DrawString("$index. ID $($e.id)   $([Math]::Round($e.width)) x $([Math]::Round($e.height)) mm",$font,$dark,650,(161+($index-1)*51))
 $g.DrawString("    max |dX / dY / dZ|: $vals mm",$small,$dark,650,(184+($index-1)*51))
}
$g.DrawString('Green: all sampled points within 10 mm. Red: review sampled mismatch.',$small,$dark,55,709)
$g.DrawString('SECTION XZ through ID 14465104: CAD transition versus straight RVT duct',$font,$dark,35,751)
function SX([double]$x) {return [single](70+($x-14531000)*0.31)}
function SZ([double]$z) {return [single](978-($z-9000)*0.13)}
$yc=5219863.022392404
$g.SetClip((New-Object System.Drawing.Rectangle 55,790,1385,190))
foreach($t in $d.primitives){
 if($t.kind -ne 'triangle' -or ($t.layer -ne 'CAD' -and $t.layer -notmatch '^RLT_')){continue}
 $vs=$t.points
 $lo=($vs | ForEach-Object {$_[0]} | Measure-Object -Minimum).Minimum
 $hi=($vs | ForEach-Object {$_[0]} | Measure-Object -Maximum).Maximum
 if($hi -lt 14531000 -or $lo -gt 14535000){continue}
 $crossings=New-Object 'System.Collections.Generic.List[double[]]'
 for($j=0;$j -lt 3;$j++) {$a=$vs[$j];$b=$vs[(($j+1)%3)];$da=$a[1]-$yc;$db=$b[1]-$yc;if(($da*$db -lt 0) -and [Math]::Abs($b[1]-$a[1]) -gt 0.000001){$f=($yc-$a[1])/($b[1]-$a[1]);$crossings.Add(@(($a[0]+$f*($b[0]-$a[0])),($a[2]+$f*($b[2]-$a[2]))))}}
 if($crossings.Count -eq 2 -and $crossings[0][1] -gt 9000 -and $crossings[0][1] -lt 10400 -and $crossings[1][1] -gt 9000 -and $crossings[1][1] -lt 10400){$g.DrawLine($blue,(SX $crossings[0][0]),(SZ $crossings[0][1]),(SX $crossings[1][0]),(SZ $crossings[1][1]))}
}
$e=$r.results | Where-Object id -eq 14465104
$xa=SX $e.centerline[0][0];$xb=SX $e.centerline[1][0];$zt=SZ ($e.centerline[0][2]+$e.height/2);$zb=SZ ($e.centerline[0][2]-$e.height/2)
$g.DrawRectangle($red,$xa,$zt,($xb-$xa),($zb-$zt))
$g.ResetClip()
$g.DrawString('Blue: actual CAD section     Red: RVT duct 14465104',$small,$dark,70,989)
$g.DrawString('X horizontal; Z vertical. Axis scales differ.',$small,$dark,1000,989)
$g.DrawString('Screening uses nearest CAD surfaces, not a proven one-to-one object match. End connections need review.',$small,$dark,650,690)
$bmp.Save("$PSScriptRoot\XYZ_result.png",[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
