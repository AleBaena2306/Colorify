from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os as _os
FONT=_os.path.join(_os.path.dirname(_os.path.abspath(__file__)),'..','..','Assets','Colorify','Resources','Fonts','Nunito-Black.ttf')
COLS=[(0x6F,0x9B,0xD8),(0x5F,0xB8,0xAE),(0x86,0xB8,0x6C),(0xE6,0xC1,0x5A),(0xE8,0x95,0x5B),(0xD9,0x6C,0x5F)]
PAPER=(0xF3,0xEC,0xE5); SURFACE=(0xE6,0xDF,0xD3); INK=(0x3B,0x38,0x36)
def shade(c,f): return tuple(int(v*f) for v in c)
def tile(size, color, letter=None, depth=0.11, radius=0.30, alpha=255, letter_color=(255,255,255)):
    """Mate tile with darker bottom edge, as in the game's UI. Returns RGBA image (size x size*(1+depth))."""
    S=4; w=size*S; d=int(size*depth*S); r=int(size*radius*S)
    im=Image.new('RGBA',(w,w+d),(0,0,0,0)); dr=ImageDraw.Draw(im)
    dr.rounded_rectangle((0,d,w-1,w+d-1),radius=r,fill=shade(color,0.80)+(alpha,))
    dr.rounded_rectangle((0,0,w-1,w-1),radius=r,fill=color+(alpha,))
    if letter:
        f=ImageFont.truetype(FONT,int(w*0.70))
        bb=dr.textbbox((0,0),letter,font=f)
        tx=(w-(bb[2]-bb[0]))/2-bb[0]; ty=(w-(bb[3]-bb[1]))/2-bb[1]
        dr.text((tx,ty),letter,font=f,fill=letter_color+(alpha,))
    return im.resize((size,size+int(size*depth)),Image.LANCZOS)
def place(base, t, cx, cy, angle=0, shadow=True):
    if angle: t=t.rotate(angle,resample=Image.BICUBIC,expand=True)
    if shadow:
        sh=Image.new('RGBA',t.size,(0,0,0,0)); a=t.split()[3].point(lambda v:int(v*0.18))
        sh.putalpha(a); sh=Image.composite(Image.new('RGBA',t.size,(60,50,40,255)),sh,a) if False else sh
        blk=Image.new('RGBA',t.size,(70,55,40,0)); blk.putalpha(a)
        blk=blk.filter(ImageFilter.GaussianBlur(max(2,t.size[0]//40)))
        base.alpha_composite(blk,(int(cx-t.size[0]/2),int(cy-t.size[1]/2+t.size[1]*0.03)))
    base.alpha_composite(t,(int(cx-t.size[0]/2),int(cy-t.size[1]/2)))
def title_tiles(word, size, gap, angles=None):
    tiles=[tile(size, COLS[i%6], ch) for i,ch in enumerate(word)]
    W=len(word)*size+(len(word)-1)*gap+size; H=int(size*1.6)
    im=Image.new('RGBA',(W,H),(0,0,0,0))
    for i,t in enumerate(tiles):
        a=angles[i] if angles else 0
        place(im,t,size*0.5+size/2+i*(size+gap),H/2,a)
    return im.crop(im.getbbox())
