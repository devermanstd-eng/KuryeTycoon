import math, numpy as np
from PIL import Image

def hexrgb(h):
    h=h.lstrip('#'); return np.array([int(h[i:i+2],16) for i in (0,2,4)],float)/255

def render(g, HEX, size=520, az=-35, el=32, ss=2, yol=None, arka=(236,232,224), kenar=0.25, normal_kenar=False, kadraj=None, pastel=False, cizgiler=None):
    """kenar: mürekkep çizgisi için derinlik farkı eşiği (m). normal_kenar: yüz yönü değişen yerlere de çizgi (oyundaki
    mürekkep geçişi gibi). kadraj: ((x0, y0, z0), (x1, y1, z1)) verilirse görüntü bu kutuya göre ortalanır (aynı modelin
    çatılı / çatısız hâli aynı kadrajda çıksın diye); yoksa modelin kendi sınırları.
    pastel: Bina Atölyesi'nin elle çizilmiş görünümünü yaklaşık gösterir (yumuşak ışık, gölgede tarama, kâğıt dokusu,
    siyah yerine yüzeyin koyusu renginde titrek kontur).
    cizgiler: (p0, p1, "#rrggbb") listesi verilirse (Bina Atölyesi'nin kenar çizgileri) derinlik konturu yerine bunlar,
    oyundaki gibi sabit piksel kalınlığında ve hafif titrek çizilir; Pastel_Cam yüzlerine çapraz parlama eklenir."""
    V=np.array(g.v,float)
    S=size*ss
    a=math.radians(az); e=math.radians(el)
    # kamera yönü (kameradan sahneye)
    fwd=np.array([math.sin(a)*math.cos(e)*-1, math.cos(a)*math.cos(e), -math.sin(e)])
    fwd=np.array([-math.sin(a)*math.cos(e), math.cos(a)*math.cos(e), -math.sin(e)])
    right=np.cross(fwd,[0,0,1]); right/=np.linalg.norm(right)
    up=np.cross(right,fwd)
    sx=V@right; sy=V@up; dz=V@fwd
    if kadraj is not None:
        (a0,b0,c0),(a1,b1,c1)=kadraj
        K=np.array([(x,y,z) for x in (a0,a1) for y in (b0,b1) for z in (c0,c1)],float)
        kx=K@right; ky=K@up
        mn=np.array([kx.min(),ky.min()]); mx=np.array([kx.max(),ky.max()])
    else:
        mn=np.array([sx.min(),sy.min()]); mx=np.array([sx.max(),sy.max()])
    sc=(S*0.92)/max(mx-mn)
    cx=(mn+mx)/2
    px=(sx-cx[0])*sc+S/2; py=S/2-(sy-cx[1])*sc
    zb=np.full((S,S),np.inf); img=np.zeros((S,S,3)); img[:]=np.array(arka)/255
    nb=np.zeros((S,S,3)) if normal_kenar else None
    sun=np.array([0.45,-0.55,0.75]); sun/=np.linalg.norm(sun)
    cols={}
    for f,mi in zip(g.f,g.fm):
        m=g.mats[mi]
        P=V[f]
        n=np.zeros(3)
        for i in range(len(f)):
            p,q=P[i],P[(i+1)%len(f)]
            n+= [(p[1]-q[1])*(p[2]+q[2]),(p[2]-q[2])*(p[0]+q[0]),(p[0]-q[0])*(p[1]+q[1])]
        ln=np.linalg.norm(n)
        if ln<1e-12: continue
        n/=ln
        if n@fwd>=0: continue
        if m not in cols: cols[m]=hexrgb(HEX[m])
        lam=max(0,n@sun)
        col=cols[m]*((0.80+0.25*lam) if pastel else (0.55+0.55*lam))
        if pastel and lam<0.25: col=col*np.array([0.97,0.95,1.0])     # gölge: yüzeyin biraz koyusu, hafif mor
        if pastel and '_Kabuk_' in m: col=cols[m]
        parlama=None
        if pastel and m.startswith('Pastel_Cam'):
            sxy=px[f]+py[f]; parlama=(sxy.min(), max(1e-6, sxy.max()-sxy.min()))
        if m.startswith(('Cam','Vitrin','OfisCam')): col=col*0.85+0.1
        col=np.clip(col,0,1)
        for k in range(1,len(f)-1):
            tri=[f[0],f[k],f[k+1]]
            x=px[tri]; y=py[tri]; z=dz[tri]
            x0=max(int(math.floor(x.min())),0); x1=min(int(math.ceil(x.max())),S-1)
            y0=max(int(math.floor(y.min())),0); y1=min(int(math.ceil(y.max())),S-1)
            if x1<x0 or y1<y0: continue
            gx,gy=np.meshgrid(np.arange(x0,x1+1)+0.5,np.arange(y0,y1+1)+0.5)
            d=(y[1]-y[2])*(x[0]-x[2])+(x[2]-x[1])*(y[0]-y[2])
            if abs(d)<1e-12: continue
            w0=((y[1]-y[2])*(gx-x[2])+(x[2]-x[1])*(gy-y[2]))/d
            w1=((y[2]-y[0])*(gx-x[2])+(x[0]-x[2])*(gy-y[2]))/d
            w2=1-w0-w1
            msk=(w0>=-1e-6)&(w1>=-1e-6)&(w2>=-1e-6)
            if not msk.any(): continue
            zz=w0*z[0]+w1*z[1]+w2*z[2]
            sub=zb[y0:y1+1,x0:x1+1]
            upd=msk&(zz<sub)
            sub[upd]=zz[upd]
            img[y0:y1+1,x0:x1+1][upd]=col
            if parlama is not None:
                tt=((gx+gy)-parlama[0])/parlama[1]
                bant=upd&(((tt>0.36)&(tt<0.43))|((tt>0.47)&(tt<0.5)))
                img[y0:y1+1,x0:x1+1][bant]=np.clip(col*0.35+0.68,0,1)
            if nb is not None: nb[y0:y1+1,x0:x1+1][upd]=n
    # mürekkep çizgisi: derinlik kenarları
    dd=np.where(np.isinf(zb),1e9,zb)
    ed=np.zeros((S,S),bool)
    for dy,dx in ((0,1),(1,0)):
        a1=dd; a2=np.roll(dd,(dy,dx),(0,1))
        ed|=np.abs(a1-a2)>kenar
        if nb is not None:
            n2=np.roll(nb,(dy,dx),(0,1))
            ed|=((nb*n2).sum(-1)<0.55)&(np.abs(a1-a2)<1e8)&((nb*nb).sum(-1)>0.5)&((n2*n2).sum(-1)>0.5)
    if cizgiler:
        rs2=np.random.RandomState(3)
        for cz in cizgiler:
            p0,p1,hx=cz[0],cz[1],cz[2]
            kal=cz[3] if len(cz)>3 else 1.0          # kademeli kalınlık (Bina Atölyesi v4): <1 ince, >1 kalın
            a=np.array(p0,float); b=np.array(p1,float)
            xa,ya,za=(a@right-cx[0])*sc+S/2, S/2-(a@up-cx[1])*sc, a@fwd
            xb,yb,zb_=(b@right-cx[0])*sc+S/2, S/2-(b@up-cx[1])*sc, b@fwd
            n=int(max(abs(xb-xa),abs(yb-ya)))+2
            t=np.linspace(0,1,n)
            # el titremesi: çizgi boyunca hafif dalga, yer yer kesinti
            dal=np.sin(t*n*0.08+rs2.rand()*6.28)*0.6*ss*0.5
            L=max(1e-6,math.hypot(xb-xa,yb-ya)); nx,ny=-(yb-ya)/L,(xb-xa)/L
            xs=xa+(xb-xa)*t+nx*dal; ys=ya+(yb-ya)*t+ny*dal; zs=za+(zb_-za)*t
            sag=np.ones(n,bool)
            if n>24 and rs2.rand()<0.35:
                k0=rs2.randint(0,n-6); sag[k0:k0+max(2,n//18)]=False
            ton=hexrgb(hx)*(0.85+0.3*rs2.rand())
            ssk=max(1,ss-1) if kal<0.9 else ss+1 if kal>1.1 else ss
            for ox in range(ssk):
                for oy in range(ssk):
                    xi=(xs+ox-ss/2+0.5).astype(int); yi=(ys+oy-ss/2+0.5).astype(int)
                    ok=sag&(xi>=0)&(xi<S)&(yi>=0)&(yi<S)
                    xi,yi,zz=xi[ok],yi[ok],zs[ok]
                    gor=zz<=zb[yi,xi]+0.09
                    img[yi[gor],xi[gor]]=img[yi[gor],xi[gor]]*0.22+ton*0.78
    if pastel:
        rs=np.random.RandomState(7)
        yy,xx=np.mgrid[0:S,0:S]
        # gölgede tarama: koyu (az ışık alan) yüzeylerde ince çapraz çizgiler
        parlak=img.max(-1)
        golge=(img.sum(-1)<img.sum(-1).mean()*0.86)&~np.isinf(zb)
        tarama=(((xx+yy)//ss)%5==0)&golge
        img[tarama]*=0.93
        ince=(((xx+yy)//ss)%9==0)&~golge&~np.isinf(zb)          # ışıkta da çok hafif kalem dokusu
        img[ince]*=0.975
        # kontur: yüzeyin kendi renginin koyusu, yer yer kesik (titrek kalem)
        kesik=rs.rand(S//8+1,S//8+1).repeat(8,0).repeat(8,1)[:S,:S]>0.12
        e2=ed&kesik&(cizgiler is None)
        img[e2]=img[e2]*0.30*np.array([0.95,0.86,0.98])+np.array([0.10,0.06,0.10])*0.2
        # kâğıt dokusu
        kagit=rs.rand(S//2+1,S//2+1).repeat(2,0).repeat(2,1)[:S,:S]
        img*=(0.965+0.07*kagit)[...,None]
        img=np.clip(img,0,1)
    else:
        img[ed]=img[ed]*0.25+np.array([0.12,0.09,0.07])*0.75
    im=Image.fromarray((img*255).astype(np.uint8)).resize((size,size),Image.LANCZOS)
    if yol: im.save(yol)
    return im
