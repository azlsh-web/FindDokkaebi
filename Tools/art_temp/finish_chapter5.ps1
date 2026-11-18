param([ValidateSet('Copies','Variants','Finalize','Preview')][string]$Mode = 'Finalize')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
# The embedded helper is compiled in memory; no game source files are changed.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
public static class Chapter5TempArt {
  public static Bitmap Load(string p) {
    using (Image src=Image.FromFile(p)) {
      Bitmap b=new Bitmap(src.Width,src.Height,PixelFormat.Format32bppArgb);
      using(Graphics g=Graphics.FromImage(b)) {g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(src,new Rectangle(0,0,src.Width,src.Height),0,0,src.Width,src.Height,GraphicsUnit.Pixel);}
      return b;
    }
  }
  static byte[] Pixels(Bitmap b) {
    BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
    byte[] a=new byte[b.Width*b.Height*4];Marshal.Copy(d.Scan0,a,0,a.Length);b.UnlockBits(d);return a;
  }
  static void Put(Bitmap b,byte[] a) {
    BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
    Marshal.Copy(a,0,d.Scan0,a.Length);b.UnlockBits(d);
  }
  static byte Clamp(double v) {return (byte)Math.Max(0,Math.Min(255,v));}
  public static void DarkCopy(string src,string dest,bool bat) {
    using(Bitmap b=Load(src)) {
      byte[] a=Pixels(b);int w=b.Width,h=b.Height;
      for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
        int i=(y*w+x)*4;if(a[i+3]==0)continue;
        double bl=a[i],gr=a[i+1],r=a[i+2],lum=.2126*r+.7152*gr+.0722*bl;
        bool skin=gr>r+9 && gr>bl+10 && gr>65;
        if(skin) {double t=Math.Max(0,Math.Min(1,(lum-70)/155));a[i+2]=Clamp(53+44*t);a[i+1]=Clamp(49+65*t);a[i]=Clamp(76+32*t);}
        else {a[i+2]=Clamp(r*.64+lum*.05);a[i+1]=Clamp(gr*.59+lum*.06);a[i]=Clamp(bl*.65+lum*.08);}
        // Restrict red-eye treatment to the existing pupil shapes and highlights.
        bool eye=bat ? ((x>=370&&x<=440&&y>=373&&y<=450)||(x>=569&&x<=640&&y>=384&&y<=465))
                     : ((x>=375&&x<=450&&y>=363&&y<=446)||(x>=555&&x<=633&&y>=382&&y<=460));
        if(eye && ((r<45&&gr<45&&bl<45)||(r>235&&gr>235&&bl>235))) {
          if(lum>220) {a[i+2]=255;a[i+1]=161;a[i]=151;}
          else {a[i+2]=239;a[i+1]=25;a[i]=49;}
        }
      }
      // Low red glow stays inside the original opaque silhouette and preserves alpha exactly.
      for(int y=340;y<485;y++) for(int x=335;x<675;x++) {
        double cx1=bat?404:414,cy1=bat?408:400,cx2=bat?604:594,cy2=bat?423:420;
        double d1=(x-cx1)*(x-cx1)/2600.0+(y-cy1)*(y-cy1)/2800.0;
        double d2=(x-cx2)*(x-cx2)/2600.0+(y-cy2)*(y-cy2)/2800.0;
        int i=(y*w+x)*4;double t=.16*Math.Exp(-Math.Min(d1,d2)*2);
        if(a[i+3]>0) {a[i+2]=Clamp(a[i+2]*(1-t)+255*t);}
      }
      Put(b,a);b.Save(dest,ImageFormat.Png);
    }
  }
  public static void SpiritCopy(string src,string dest) {
    using(Bitmap b=Load(src)) {
      int w=b.Width,h=b.Height;byte[] a=Pixels(b),o=new byte[a.Length];
      // A small feathered halo and 55% source opacity, without moving the drawing.
      for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
        int i=(y*w+x)*4;double l=.2126*a[i+2]+.7152*a[i+1]+.0722*a[i];
        double halo=0,weight=0;
        for(int dy=-8;dy<=8;dy+=2) for(int dx=-8;dx<=8;dx+=2) {
          int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;
          double k=Math.Exp(-(dx*dx+dy*dy)/28.0);halo+=a[(yy*w+xx)*4+3]*k;weight+=k;
        }
        double sa=a[i+3]*.55,ga=(halo/Math.Max(1,weight))*.18*(1-a[i+3]/255.0);
        o[i+3]=Clamp(sa+ga);if(o[i+3]>0){o[i]=Clamp(207+l*.19);o[i+1]=Clamp(126+l*.44);o[i+2]=Clamp(65+l*.61);}
      }
      Put(b,o);b.Save(dest,ImageFormat.Png);
    }
  }
  public static void Normalize(string path,int w,int h,bool opaque,bool trim) {
    using(Bitmap src=Load(path)) using(Bitmap dst=new Bitmap(w,h,PixelFormat.Format32bppArgb)) {
      Rectangle box=new Rectangle(0,0,src.Width,src.Height);
      if(trim) {
        byte[] a=Pixels(src);int x0=src.Width,y0=src.Height,x1=-1,y1=-1;
        for(int y=0;y<src.Height;y++)for(int x=0;x<src.Width;x++)if(a[(y*src.Width+x)*4+3]>12){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
        if(x1>=0)box=new Rectangle(x0,y0,x1-x0+1,y1-y0+1);
      }
      using(Graphics g=Graphics.FromImage(dst)) {
        g.Clear(opaque?Color.FromArgb(255,25,30,55):Color.Transparent);
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        double scale=trim?Math.Min(w*.80/box.Width,h*.80/box.Height):Math.Max((double)w/box.Width,(double)h/box.Height);
        float dw=(float)(box.Width*scale),dh=(float)(box.Height*scale);
        g.DrawImage(src,new RectangleF((w-dw)/2,(h-dh)/2,dw,dh),box,GraphicsUnit.Pixel);
      }
      // Release the source handle before replacing our newly generated output.
      src.Dispose();dst.Save(path,ImageFormat.Png);
    }
  }
  public static void ColorVariant(string src,string dest,string kind) {
    using(Bitmap b=Load(src)) {
      byte[] a=Pixels(b);
      for(int i=0;i<a.Length;i+=4) {
        if(a[i+3]==0)continue;double r=a[i+2],g=a[i+1],bl=a[i],l=.2126*r+.7152*g+.0722*bl,t=l/255.0;
        bool change=false;double nr=r,ng=g,nb=bl;
        if(kind=="Jipsin"&&l>90) {change=true;nr=28+72*t;ng=85+135*t;nb=140+105*t;}
        if(kind=="Bujeok"&&r>130&&g>85&&bl<g*.8) {change=true;nr=50+35*t;ng=110+50*t;nb=215+35*t;}
        if(kind=="Buchae"&&(g>150||bl>150)) {change=true;nr=117+80*t;ng=75+60*t;nb=175+65*t;}
        if(kind=="Buchae"&&r>170&&g<135&&bl<120) {change=true;nr=225;ng=105;nb=179;}
        if(kind=="DakpiBowl"&&r>120&&r>g*1.8&&r>bl*1.5) {change=true;nr=35;ng=125+60*t;nb=235;}
        if(kind=="Gamtu"&&Math.Abs(r-bl)<25&&Math.Abs(g-bl)<25&&l>18) {change=true;double q=Math.Min(1,l/80);nr=180+70*q;ng=65+75*q;nb=135+60*q;}
        if(kind=="PatBag"&&r>g*1.3&&r>bl*1.3&&r>65&&!(g>120&&bl>70)) {change=true;nr=45+50*t;ng=95+95*t;nb=42+40*t;}
        if(change) {a[i+2]=Clamp(nr);a[i+1]=Clamp(ng);a[i]=Clamp(nb);}
      }
      Put(b,a);b.Save(dest,ImageFormat.Png);
    }
  }
  public static void SolidHeart(string path) {
    using(Bitmap b=Load(path)) {
      byte[] a=Pixels(b);int w=b.Width,h=b.Height;bool[] outside=new bool[w*h];Queue<int> q=new Queue<int>();
      for(int x=0;x<w;x++){q.Enqueue(x);q.Enqueue((h-1)*w+x);}
      for(int y=1;y<h-1;y++){q.Enqueue(y*w);q.Enqueue(y*w+w-1);}
      while(q.Count>0) {
        int p=q.Dequeue();if(outside[p]||a[p*4+3]>=220)continue;outside[p]=true;
        int x=p%w,y=p/w;if(x>0)q.Enqueue(p-1);if(x+1<w)q.Enqueue(p+1);if(y>0)q.Enqueue(p-w);if(y+1<h)q.Enqueue(p+w);
      }
      for(int p=0;p<w*h;p++)if(!outside[p]) {
        int i=p*4;double r=a[i+2],g=a[i+1],bl=a[i];
        if((r>100&&r>g*2&&r>bl*2)||(a[i+3]<220&&g<80&&bl<80)){a[i+2]=239;a[i+1]=67;a[i]=75;}
        a[i+3]=255;
      }
      Put(b,a);b.Save(path,ImageFormat.Png);
    }
  }
  public static string Info(string path) {
    using(Bitmap b=Load(path)) {
      byte[] a=Pixels(b);int min=255,max=0,x0=b.Width,y0=b.Height,x1=-1,y1=-1;
      for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) {int al=a[(y*b.Width+x)*4+3];min=Math.Min(min,al);max=Math.Max(max,al);if(al>12){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}}
      return b.Width+"x"+b.Height+" RGBA alpha="+min+".."+max+" bounds="+x0+","+y0+","+x1+","+y1;
    }
  }
  public static int AlphaDifferences(string original,string copy) {
    using(Bitmap a=Load(original))using(Bitmap b=Load(copy)) {
      if(a.Size!=b.Size)return -1;byte[] aa=Pixels(a),bb=Pixels(b);int differences=0;
      for(int i=3;i<aa.Length;i+=4)if(aa[i]!=bb[i])differences++;return differences;
    }
  }
}
'@
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$artRoot = Join-Path $repoRoot 'Assets\Sprites\Chapter5'
if ($Mode -eq 'Copies') {
  [Chapter5TempArt]::DarkCopy((Join-Path $repoRoot 'Assets\Sprites\Characters\Char_Dokkaebi_NoBat.png'), (Join-Path $artRoot 'Char_Dokkaebi_Dark.png'), $false)
  [Chapter5TempArt]::DarkCopy((Join-Path $repoRoot 'Assets\Sprites\Characters\Char_Dokkaebi_Normal.png'), (Join-Path $artRoot 'Char_Dokkaebi_DarkBat.png'), $true)
  [Chapter5TempArt]::SpiritCopy((Join-Path $repoRoot 'Assets\Sprites\Stage5_Friend\Friend_Original.png'), (Join-Path $artRoot 'Char_Friend_Spirit.png'))
  Write-Output ('NoBat alpha differences: '+[Chapter5TempArt]::AlphaDifferences((Join-Path $repoRoot 'Assets\Sprites\Characters\Char_Dokkaebi_NoBat.png'),(Join-Path $artRoot 'Char_Dokkaebi_Dark.png')))
  Write-Output ('Bat alpha differences: '+[Chapter5TempArt]::AlphaDifferences((Join-Path $repoRoot 'Assets\Sprites\Characters\Char_Dokkaebi_Normal.png'),(Join-Path $artRoot 'Char_Dokkaebi_DarkBat.png')))
} elseif ($Mode -eq 'Variants') {
  @('Jipsin','Bujeok','Buchae','DakpiBowl','Gamtu','PatBag') | ForEach-Object {
    [Chapter5TempArt]::ColorVariant((Join-Path $artRoot ('Ch5_'+$_+'_Original.png')),(Join-Path $artRoot ('Ch5_'+$_+'_Wrong1.png')), $_)
  }
} elseif ($Mode -eq 'Finalize') {
  Get-ChildItem -LiteralPath $artRoot -Filter '*.png' -File | ForEach-Object {
    $n=$_.Name
    if($n -match '^Ch[15]_') {[Chapter5TempArt]::Normalize($_.FullName,1024,1024,$false,$true)}
    elseif($n -match '^BG_') {[Chapter5TempArt]::Normalize($_.FullName,1124,1976,$true,$false)}
    elseif($n -eq 'HUD_Heart.png') {[Chapter5TempArt]::SolidHeart($_.FullName);[Chapter5TempArt]::Normalize($_.FullName,256,256,$false,$true)}
    elseif($n -eq 'Chapter_Forest.png') {[Chapter5TempArt]::Normalize($_.FullName,1760,1286,$true,$false)}
    elseif($n -eq 'GameOver_Silhouette.png') {[Chapter5TempArt]::Normalize($_.FullName,1080,700,$false,$true)}
    elseif($n -eq 'Scare_Face.png') {[Chapter5TempArt]::Normalize($_.FullName,1080,1080,$false,$true)}
    Write-Output ($n + ' : ' + [Chapter5TempArt]::Info($_.FullName))
  }
} elseif ($Mode -eq 'Preview') {
  $artFiles=@(Get-ChildItem -LiteralPath $artRoot -Filter '*.png' -File | Sort-Object Name)
  $cols=7;$cw=230;$ch=205;$rows=[int][Math]::Ceiling($artFiles.Count/$cols)
  $sheet=New-Object System.Drawing.Bitmap ($cols*$cw),($rows*$ch)
  $gr=[System.Drawing.Graphics]::FromImage($sheet);$gr.Clear([System.Drawing.Color]::FromArgb(240,234,224))
  $font=New-Object System.Drawing.Font 'Arial',9
  for($i=0;$i -lt $artFiles.Count;$i++) {
    $src=[Chapter5TempArt]::Load($artFiles[$i].FullName)
    $s=[Math]::Min(210/$src.Width,170/$src.Height);$dw=[int]($src.Width*$s);$dh=[int]($src.Height*$s)
    $xx=($i%$cols)*$cw;$yy=[int][Math]::Floor($i/$cols)*$ch
    $gr.DrawImage($src, [int]($xx+($cw-$dw)/2),[int]($yy+(175-$dh)/2),$dw,$dh)
    $gr.DrawString($artFiles[$i].BaseName,$font,[System.Drawing.Brushes]::Black,[single]($xx+4),[single]($yy+178));$src.Dispose()
  }
  $sheet.Save((Join-Path $PSScriptRoot 'chapter5_contact_sheet.png'),[System.Drawing.Imaging.ImageFormat]::Png)
  $font.Dispose();$gr.Dispose();$sheet.Dispose()
}
