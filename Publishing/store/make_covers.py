from PIL import Image, ImageDraw, ImageFont
import random
COLS=[(0x6F,0x9B,0xD8),(0x5F,0xB8,0xAE),(0x86,0xB8,0x6C),(0xE6,0xC1,0x5A),(0xE8,0x95,0x5B),(0xD9,0x6C,0x5F)]
FONT='/home/claude/colorify/Assets/Colorify/Resources/Fonts/Nunito-Black.ttf'
def title(size):
    f=ImageFont.truetype(FONT,size); word='COLORIFY'; sp=int(size*0.06)
    ws=[f.getbbox(ch)[2]-f.getbbox(ch)[0] for ch in word]
    asc,desc=f.getmetrics()
    W=sum(f.getlength(ch) for ch in word)+sp*(len(word)-1)
    im=Image.new('RGBA',(int(W)+4,asc+desc),(0,0,0,0)); d=ImageDraw.Draw(im); x=0
    for i,ch in enumerate(word):
        d.text((x,0),ch,font=f,fill=COLS[i%6]+(255,)); x+=f.getlength(ch)+sp
    return im.crop(im.getbbox())
src=Image.open('/home/claude/portfolio/content/games/colorify/cover.jpg').convert('RGB')
bg=src.getpixel((160,160)); panel=src.getpixel((400,345))
tiles=[src.crop((c*160,r*160,c*160+160,r*160+160)) for r in (0,1,3,4) for c in range(5)]
text=src.crop((140,352,662,446))
def make(W,H,cell,title_w,seed):
    random.seed(seed)
    im=Image.new('RGB',(W,H),bg)
    cols=-(-W//cell)+1; rows=-(-H//cell)+1
    ox=(W-cols*cell)//2; oy=(H-rows*cell)//2
    t0=title(200); th0=int(t0.height*title_w/t0.width)
    pw0,ph0=int(title_w*1.2),int(th0*2.3); m=max(10,cell//6)
    hole=((W-pw0)//2-m,(H-ph0)//2-m,(W+pw0)//2+m,(H+ph0)//2+m)
    bag=[]
    for r in range(rows):
        for c in range(cols):
            x0,y0=ox+c*cell,oy+r*cell
            if x0<hole[2] and x0+cell>hole[0] and y0<hole[3] and y0+cell>hole[1]: continue
            if not bag: bag.extend(random.sample(tiles,len(tiles)))
            t=bag.pop()
            im.paste(t.resize((cell,cell),Image.LANCZOS),(ox+c*cell,oy+r*cell))
    t=title(200); t=t.resize((title_w,int(t.height*title_w/t.width)),Image.LANCZOS)
    tw,th=t.size
    pw,ph=int(tw*1.2),int(th*2.3)
    px,py=(W-pw)//2,(H-ph)//2
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((px-14,py-14,px+pw+14,py+ph+14),radius=int(ph*0.33),fill=bg)
    d.rounded_rectangle((px,py,px+pw,py+ph),radius=int(ph*0.28),fill=panel)
    im.paste(t,((W-tw)//2,(H-th)//2),t)
    return im
