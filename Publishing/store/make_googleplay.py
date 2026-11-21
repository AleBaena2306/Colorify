# Genera los gráficos de Google Play de Colorify (21/11/2026). Uso: python3 make_googleplay.py
#   googleplay/icon-512.png               icono de la ficha (512x512, PNG con alfa)
#   googleplay/feature-graphic-1024x500.png  gráfico destacado (sin alfa)
#   ../../Assets/Colorify/Art/AppIcon/*.png  icono de la app (normal y capas del adaptativo, 1024)
# Las capturas (googleplay/screenshots, 1080x1920) salen de la grabación del móvil del 11/11 (1080x2424):
#   ffmpeg -ss <t> -i grabacion.mp4 -frames:v 1 -vf "crop=1080:1920:0:193" captura.png
#   (t = 87.6 dark stripes, 96.0 spin, 94.0 remember it, 79.3 stripes) y de capturas de la UI recortadas a
#   1080x1920 centrando el contenido (menú y=220, colección y=281, ranking y=267).
import random, os
from PIL import Image, ImageDraw, ImageFont
from gp_tools import *
HERE=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(HERE,'googleplay'); ICON=os.path.join(HERE,'..','..','Assets','Colorify','Art','AppIcon')

def icon(full=True):
    S=1024
    im=Image.new('RGBA',(S,S),PAPER+(255,) if full else (0,0,0,0))
    if full:
        place(im, tile(260,COLS[3],alpha=70), 870, 150, -14, shadow=False)
        place(im, tile(230,COLS[1],alpha=70), 150, 880, 12, shadow=False)
        place(im, tile(170,COLS[5],alpha=60), 905, 905, 8, shadow=False)
        place(im, tile(150,COLS[2],alpha=60), 120, 130, -10, shadow=False)
    place(im, tile(620 if full else 470,COLS[0],'C'), S/2, S/2+(10 if full else 0), -6)
    return im

def shape(kind,size,color,alpha):
    S=size*3; s=Image.new('RGBA',(S,S),(0,0,0,0)); d=ImageDraw.Draw(s); c=color+(alpha,)
    if kind=='circle': d.ellipse((0,0,S-1,S-1),fill=c)
    elif kind=='tri': d.polygon([(S/2,0),(S-1,S-1),(0,S-1)],fill=c)
    elif kind=='dia': d.polygon([(S/2,0),(S-1,S/2),(S/2,S-1),(0,S/2)],fill=c)
    return s.resize((size,size),Image.LANCZOS)

def feature(tagline='Tap the matching color. Fast.'):
    random.seed(7); W,H=2048,1000
    im=Image.new('RGBA',(W,H),PAPER+(255,))
    spots=[(120,140),(330,820),(560,110),(1500,120),(1740,860),(1930,210),(250,480),(1820,520),(980,900),(1180,70),(40,860),(2000,950)]
    for i,(x,y) in enumerate(spots):
        k=['tile','circle','tri','dia'][i%4]; sz=random.randint(130,210); col=COLS[i%6]
        if k=='tile': place(im,tile(sz,col,alpha=80),x,y,random.randint(-15,15),shadow=False)
        else: place(im,shape(k,sz,col,80),x,y,random.randint(-15,15),shadow=False)
    logo=title_tiles('COLORIFY',190,18,angles=[-5,3,-3,4,-4,3,-3,5])
    logo=logo.resize((1560,int(logo.height*1560/logo.width)),Image.LANCZOS)
    place(im,logo,W/2,H/2-70,0,shadow=False)
    f=ImageFont.truetype(FONT.replace('Black','ExtraBold'),88); d=ImageDraw.Draw(im)
    bb=d.textbbox((0,0),tagline,font=f); tw=bb[2]-bb[0]; px,py=(W-tw)/2-50,H/2+150
    d.rounded_rectangle((px,py,px+tw+100,py+140),radius=70,fill=SURFACE+(255,))
    d.text(((W-tw)/2-bb[0],py+70-(bb[1]+bb[3])/2),tagline,font=f,fill=INK+(255,))
    return im.convert('RGB').resize((1024,500),Image.LANCZOS)

if __name__=='__main__':
    os.makedirs(OUT,exist_ok=True)
    full=icon(True); full.save(os.path.join(ICON,'AppIcon.png')); full.resize((512,512),Image.LANCZOS).save(os.path.join(OUT,'icon-512.png'))
    icon(False).save(os.path.join(ICON,'AppIcon-Foreground.png'))
    Image.new('RGBA',(1024,1024),PAPER+(255,)).save(os.path.join(ICON,'AppIcon-Background.png'))
    feature().save(os.path.join(OUT,'feature-graphic-1024x500.png'))
