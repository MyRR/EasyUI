/**
 * @fileoverview �Ӷ���ͼƬ��ѡ��һ����Ϊ����ͼƬ������ͼ��
 * @author ��Ӣ�����ӣ�<daxingplay@gmail.com>������<jianping.xwh@taobao.com>

 */
KISSY.add('gallery/form/1.3/uploader/plugins/coverPic/coverPic', function(S, Node,Base){

    var $ = Node.all,
        LOG_PRE = '[LineQueue: setMainPic] ';

    /**
     * �Ӷ���ͼƬ��ѡ��һ����Ϊ����ͼƬ������ͼ
     * @param {NodeList | String} $input Ŀ��Ԫ��
     * @param {Uploader} uploader uploader��ʵ��
     * @constructor
     */
    function CoverPic($input,uploader){

    }
    S.extend(CoverPic, Base, /** @lends CoverPic.prototype*/{
        /**
         * �������
         */
        render:function(){

        }
    },{
        ATTRS:/** @lends CoverPic.prototype*/{

        }
    });

    return CoverPic;

}, {
    requires: [ 'node','base' ]
});