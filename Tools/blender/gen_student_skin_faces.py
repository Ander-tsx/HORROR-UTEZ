"""Original stylized faces for four identities; preserve the existing Erick portrait.
No claim of likeness for students without reference photographs. Uses the game's
existing face atlas and cylindrical back seam layout, not external images.
"""
import bpy,numpy as np,os
root='Assets/_Project/Art/Remake/Resources/Skins'
os.makedirs(root,exist_ok=True)
img=bpy.data.images.load(os.path.abspath('Assets/_Project/Art/Textures/Player/T_Player_Head.png'))
w,h=img.size
portrait=np.array(img.pixels[:],dtype=np.float32).reshape(h,w,4)
y,x=np.mgrid[0:h,0:w];u=x/w;v=y/h
for name,skin,hair in [('Ander',( .66,.45,.34),(.07,.045,.025)),('Erick',(.72,.51,.4),(.03,.025,.02)),('Cesar',(.58,.36,.25),(.018,.018,.018)),('Juan',(.77,.57,.45),(.12,.065,.027)),('Sebas',(.68,.47,.34),(.025,.022,.023))]:
    if name=='Erick':pixels=portrait.copy()
    else:
        pixels=np.ones_like(portrait);pixels[:,:,:3]=skin
        face=((u-.5)/.27)**2+((v-.43)/.42)**2<1
        hairline=.57+.025*np.cos(u*36+(2 if name=='Juan' else 0))
        pixels[v>hairline,:3]=hair
        # Soft cheek/nose shading retains a continuous face, rather than a pasted square.
        shade=.88+.12*np.exp(-((u-.5)/.22)**2)
        pixels[:,:,:3]*=shade[:,:,None]
        for eye in (.405,.595):
            mask=((u-eye)/.025)**2+((v-.35)/.020)**2<1
            pixels[mask,:3]=(.84,.78,.68)
            pupil=((u-eye)/.010)**2+((v-.351)/.014)**2<1;pixels[pupil,:3]=(.025,.026,.024)
            brow=(abs(u-eye)<.041)&(abs(v-(.41+(u-eye)*(.3 if name=='Cesar' else -.15)))<.012)
            pixels[brow,:3]=hair
        nose=(abs(u-.5)<.018)&(v>.19)&(v<.27);pixels[nose,:3]=np.array(skin)*.77
        mouth=(abs(u-.5)<(.058 if name=='Sebas' else .05))&(abs(v-.075)<.009);pixels[mouth,:3]=(.30,.135,.10)
        if name=='Cesar':pixels[(abs(u-.5)<.09)&(v<.12),:3]*=.68
        if name=='Ander':
            glasses=((abs(u-.405)<.065)|(abs(u-.595)<.065))&(abs(v-.35)<.045)
            inner=((abs(u-.405)<.053)|(abs(u-.595)<.053))&(abs(v-.35)<.033)
            pixels[glasses&~inner,:3]=(.08,.075,.07)
        pixels[:,:,3]=1
    out=bpy.data.images.new('Skin_'+name,width=w,height=h,alpha=True)
    out.pixels=pixels.ravel().tolist();out.filepath_raw=os.path.abspath(root+'/Skin_'+name+'.png');out.file_format='PNG';out.save()
print('SKINS_EXPORT',root)
